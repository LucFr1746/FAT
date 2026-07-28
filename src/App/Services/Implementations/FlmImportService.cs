using System.Diagnostics;
using Data;
using Domain.Constants;
using Microsoft.EntityFrameworkCore;
using Services.Abstractions;
using Services.Dtos;
using Services.Import;

namespace Services.Implementations;

/// <summary>
/// Imports the FLM catalog export. See <see cref="IFlmImportService"/> for the
/// idempotency contract this must honour.
///
/// The per-entity upsert logic lives in FlmImportService.Upserts.cs; this file
/// holds the public API, reader selection and the transaction.
/// </summary>
public sealed partial class FlmImportService : IFlmImportService
{
    private readonly FAT_DBContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IReadOnlyList<IFlmDataReader> _readers;

    public FlmImportService(FAT_DBContext db, ICurrentUserContext currentUser)
        : this(db, currentUser, [new XlsxFlmDataReader(), new JsonFlmDataReader(), new SingleCurriculumJsonDataReader()])
    {
    }

    /// <summary>Overload used by tests to supply a stub reader.</summary>
    internal FlmImportService(
        FAT_DBContext db,
        ICurrentUserContext currentUser,
        IReadOnlyList<IFlmDataReader> readers)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _readers = readers ?? throw new ArgumentNullException(nameof(readers));
    }

    public async Task<ImportPreviewDto> PreviewAsync(
        string path, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Xem trước dữ liệu import");

        var reader = SelectReader(path);
        var data = await reader.ReadAsync(path, cancellationToken);
        var warnings = Validate(data);

        return new ImportPreviewDto(
            SourceName: reader.SourceName,
            FilePath: path,
            MajorCount: data.Subjects.Select(s => s.MajorCode).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            SubjectCount: data.Subjects.Select(s => s.SubjectCode).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            CurriculumLinkCount: data.Subjects.Count,
            AssessmentCount: data.Assessments.Count(a => !a.IsSubComponent),
            MaterialCount: data.Materials.Count,
            ScheduleCount: data.Schedules.Count,
            Warnings: warnings);
    }

    public async Task<ImportResultDto> ImportAsync(
        string path,
        ImportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Import dữ liệu chương trình học");

        options ??= ImportOptions.Default;
        var stopwatch = Stopwatch.StartNew();

        FlmDataSet data;
        try
        {
            var reader = SelectReader(path);
            data = await reader.ReadAsync(path, cancellationToken);
        }
        catch (Exception ex)
        {
            // A file that cannot be read is an expected outcome, not a crash:
            // the user picked the wrong file and needs to be told which one.
            return ImportResultDto.Failure(
                $"Không đọc được tệp dữ liệu: {ex.Message}", stopwatch.Elapsed);
        }

        if (data.Subjects.Count == 0)
        {
            return ImportResultDto.Failure(
                "Tệp không chứa môn học nào. Hãy kiểm tra lại nguồn dữ liệu.", stopwatch.Elapsed);
        }

        // One transaction for the whole import. Partway-through is the worst
        // possible state: Major.RequiredCredits would no longer match the
        // curriculum, and every graduation percentage would be wrong until
        // somebody noticed.
        //
        // The whole attempt - transaction included - runs through the
        // configured execution strategy (SqlServerRetryingExecutionStrategy,
        // see DataServiceCollectionExtensions), because EF Core forbids a
        // manually-opened transaction under a retrying strategy: a retry has
        // to be able to redo BeginTransaction itself. ImportSession is
        // recreated on every attempt for the same reason - a retried attempt
        // must not double-count what the failed attempt already counted.
        //
        // Skipped on a non-relational provider, which is the only reason the
        // in-memory unit tests can exercise this method at all.
        var strategy = _db.Database.CreateExecutionStrategy();
        ImportSession session;

        try
        {
            session = await strategy.ExecuteAsync(async () =>
            {
                var attemptSession = new ImportSession(Validate(data));

                var useTransaction = _db.Database.IsRelational();
                await using var transaction = useTransaction
                    ? await _db.Database.BeginTransactionAsync(cancellationToken)
                    : null;

                try
                {
                    var majorsByCode = await UpsertMajorsAsync(data, attemptSession, cancellationToken);
                    var coursesByCode = await UpsertCoursesAsync(data, options, attemptSession, cancellationToken);

                    await EnsureTermsAsync(data, cancellationToken);
                    await UpsertCurriculumLinksAsync(
                        data, majorsByCode, coursesByCode, options, attemptSession, cancellationToken);

                    if (options.ImportAssessments)
                    {
                        await UpsertAssessmentsAsync(data, coursesByCode, options, attemptSession, cancellationToken);
                    }

                    if (options.ImportMaterials)
                    {
                        await UpsertMaterialsAsync(data, coursesByCode, options, attemptSession, cancellationToken);
                    }

                    if (options.ImportSchedules)
                    {
                        await UpsertSchedulesAsync(data, coursesByCode, options, attemptSession, cancellationToken);
                    }

                    if (options.ImportPrerequisites)
                    {
                        await UpsertPrerequisitesAsync(data, coursesByCode, attemptSession, cancellationToken);
                    }

                    // Must happen inside the transaction: a curriculum whose
                    // credits were never resynced is exactly the drift this
                    // guards against.
                    foreach (var majorId in majorsByCode.Values)
                    {
                        await MajorCreditCalculator.SyncAsync(_db, majorId, cancellationToken);
                    }

                    await _db.SaveChangesAsync(cancellationToken);

                    if (transaction is not null)
                    {
                        await transaction.CommitAsync(cancellationToken);
                    }
                }
                catch
                {
                    if (transaction is not null)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                    }

                    throw;
                }

                return attemptSession;
            });
        }
        catch (Exception ex)
        {
            return ImportResultDto.Failure(
                $"Import thất bại và đã được hoàn tác: {ex.Message}", stopwatch.Elapsed);
        }

        stopwatch.Stop();
        return session.ToResult(stopwatch.Elapsed);
    }

    /// <summary>
    /// Picks the reader that handles this path.
    ///
    /// The message names both supported shapes because "unsupported file" alone
    /// leaves the user guessing whether to pick the workbook or the folder.
    /// </summary>
    private IFlmDataReader SelectReader(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Đường dẫn tệp dữ liệu không được để trống.", nameof(path));
        }

        return _readers.FirstOrDefault(r => r.CanRead(path))
            ?? throw new NotSupportedException(
                $"Không hỗ trợ nguồn dữ liệu '{path}'. " +
                "Hãy chọn tệp .xlsx hoặc thư mục chứa các tệp .json (subjects.json, assessments.json, ...).");
    }

    /// <summary>
    /// Checks the data before it is written and returns warnings.
    ///
    /// Warnings, not errors: FLM data has real imperfections and refusing the
    /// whole import over one odd subject would mean never importing anything.
    /// The administrator sees the list and fixes those rows in the admin screen.
    /// </summary>
    private static IReadOnlyList<string> Validate(FlmDataSet data)
    {
        var warnings = new List<string>();

        var weightBySubject = data.Assessments
            .Where(a => !a.IsSubComponent)
            .GroupBy(a => a.SubjectCode, StringComparer.OrdinalIgnoreCase)
            .Select(g => new { SubjectCode = g.Key, Total = g.Sum(a => a.WeightPercent) })
            .Where(x => Math.Abs(x.Total - 100m) > 0.5m)
            .ToList();

        foreach (var subject in weightBySubject.Take(10))
        {
            warnings.Add(
                $"Môn {subject.SubjectCode}: tổng trọng số các cột điểm là {subject.Total:0.#}% (khác 100%).");
        }

        if (weightBySubject.Count > 10)
        {
            warnings.Add($"... và {weightBySubject.Count - 10} môn khác có tổng trọng số khác 100%.");
        }

        var badCredits = data.Subjects
            .Where(s => s.Credits < CatalogRules.MinCredits || s.Credits > CatalogRules.MaxCredits)
            .Select(s => s.SubjectCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var code in badCredits.Take(10))
        {
            warnings.Add($"Môn {code}: số tín chỉ nằm ngoài khoảng " +
                         $"{CatalogRules.MinCredits}-{CatalogRules.MaxCredits} và sẽ bị bỏ qua.");
        }

        var badTerms = data.Subjects
            .Where(s => s.TermNo < CatalogRules.MinTermNo || s.TermNo > CatalogRules.MaxTermNo)
            .Select(s => $"{s.MajorCode}/{s.SubjectCode} (kỳ {s.TermNo})")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var item in badTerms.Take(10))
        {
            warnings.Add($"Bỏ qua {item}: số kỳ nằm ngoài khoảng " +
                         $"{CatalogRules.MinTermNo}-{CatalogRules.MaxTermNo}.");
        }

        return warnings;
    }
}
