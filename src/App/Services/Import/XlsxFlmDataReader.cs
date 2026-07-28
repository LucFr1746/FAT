using ClosedXML.Excel;
using Domain.Constants;

namespace Services.Import;

/// <summary>
/// Reads the FLM export from the curated workbook
/// (db/data/flm_chuong_trinh_hoc.xlsx).
///
/// Sheet layout:
///   "Mục lục"      table of contents - skipped, the sheet names carry the same
///                  information
///   one sheet per programme, NAMED AFTER THE MAJOR CODE - the subject list
///   "Combo"        elective subjects and which programme offers them
///   "TaiLieu"      bibliography and download links
///   "BangDiem"     grade structure
///   "LichKiemTra"  the assessment timeline
/// </summary>
public sealed class XlsxFlmDataReader : IFlmDataReader
{
    private const string TableOfContentsSheet = "Mục lục";
    private const string ComboSheet = "Combo";
    private const string MaterialsSheet = "TaiLieu";
    private const string GradeStructureSheet = "BangDiem";
    private const string ScheduleSheet = "LichKiemTra";

    /// <summary>Sheets that are not a programme's subject list.</summary>
    private static readonly HashSet<string> NonCurriculumSheets =
        new(StringComparer.OrdinalIgnoreCase)
        {
            TableOfContentsSheet, ComboSheet, MaterialsSheet, GradeStructureSheet, ScheduleSheet
        };

    /// <summary>Row 1 is a banner, row 2 the header, so subject data starts at row 3.</summary>
    private const int CurriculumFirstDataRow = 3;

    /// <summary>The other sheets put their header on row 1.</summary>
    private const int FlatSheetFirstDataRow = 2;

    public string SourceName => "Excel (.xlsx)";

    public bool CanRead(string path)
        => !string.IsNullOrWhiteSpace(path)
           && File.Exists(path)
           && string.Equals(Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase);

    public Task<FlmDataSet> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Không tìm thấy tệp Excel: {path}", path);
        }

        // ClosedXML is entirely synchronous. Pushing it onto the thread pool
        // keeps the WPF UI thread responsive while a few thousand rows load.
        return Task.Run(() => Read(path, cancellationToken), cancellationToken);
    }

    private static FlmDataSet Read(string path, CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook(path);

        var curricula = new List<FlmCurriculumRow>();
        var subjects = new List<FlmSubjectRow>();

        // Several cohort sheets (BIT_SE_K19..., BIT_SE_K20...) collapse to the
        // same major, so the programme is only listed once even though its
        // subjects are read from every sheet.
        var seenMajors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sheet in workbook.Worksheets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (NonCurriculumSheets.Contains(sheet.Name))
            {
                continue;
            }

            var (majorCode, majorName) = FlmValueParser.MajorFromCurriculum(sheet.Name.Trim());
            if (seenMajors.Add(majorCode))
            {
                curricula.Add(new FlmCurriculumRow(majorCode, majorName));
            }

            subjects.AddRange(ReadCurriculumSheet(sheet, majorCode));
        }

        // Combo subjects belong to a programme too - they are simply optional.
        subjects.AddRange(ReadComboSheet(workbook));

        return new FlmDataSet(
            curricula,
            subjects,
            ReadGradeStructure(workbook),
            ReadMaterials(workbook),
            ReadSchedule(workbook));
    }

    /// <summary>
    /// One programme sheet. Columns:
    /// STT | Mã môn | Tên (EN) | Tên (VN) | Kỳ | Tín chỉ | Tính GPA |
    /// Môn tiên quyết | Số bài KT | Tổng % | Bảng điểm | Tài liệu | Description
    /// </summary>
    private static IEnumerable<FlmSubjectRow> ReadCurriculumSheet(IXLWorksheet sheet, string majorCode)
    {
        foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() >= CurriculumFirstDataRow))
        {
            var subjectCode = FlmValueParser.Clean(Cell(row, 2));
            if (subjectCode is null)
            {
                continue;
            }

            var termNo = FlmValueParser.ParseIntOrNull(Cell(row, 5));
            if (termNo is null)
            {
                continue;
            }

            // Vietnamese name when there is one, otherwise English - the FLM
            // sheets fill one column or the other, rarely both.
            var name = FlmValueParser.Clean(Cell(row, 4)) ?? FlmValueParser.Clean(Cell(row, 3)) ?? subjectCode;

            yield return new FlmSubjectRow(
                MajorCode: majorCode,
                SubjectCode: subjectCode.ToUpperInvariant(),
                SubjectName: FlmValueParser.Truncate(name, CatalogRules.CourseNameMaxLength)!,
                TermNo: termNo.Value,
                Credits: FlmValueParser.ParseIntOrNull(Cell(row, 6)) ?? 0,
                CountsTowardGpa: FlmValueParser.ParseCountsGpa(Cell(row, 7)),
                PrerequisiteText: FlmValueParser.Truncate(Cell(row, 8), CatalogRules.DescriptionMaxLength),
                Description: FlmValueParser.Clean(Cell(row, 13)),
                SyllabusCode: null,
                MinAvgMarkToPass: null);
        }
    }

    /// <summary>
    /// The Combo sheet. Columns:
    /// Ngành/lớp | Slot | Kỳ | Tên combo | Mã môn | Tên (EN) | Tên (VN) |
    /// Tín chỉ | Tính GPA | Số bài KT | Tổng % | Bảng điểm | sylid
    /// </summary>
    private static IEnumerable<FlmSubjectRow> ReadComboSheet(IXLWorkbook workbook)
    {
        if (!workbook.TryGetWorksheet(ComboSheet, out var sheet))
        {
            yield break;
        }

        foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() >= FlatSheetFirstDataRow))
        {
            var rawCurriculum = FlmValueParser.Clean(Cell(row, 1));
            var subjectCode = FlmValueParser.Clean(Cell(row, 5));
            var termNo = FlmValueParser.ParseIntOrNull(Cell(row, 3));

            if (rawCurriculum is null || subjectCode is null || termNo is null)
            {
                continue;
            }

            var majorCode = FlmValueParser.MajorFromCurriculum(rawCurriculum).Code;

            var name = FlmValueParser.Clean(Cell(row, 7)) ?? FlmValueParser.Clean(Cell(row, 6)) ?? subjectCode;

            yield return new FlmSubjectRow(
                MajorCode: majorCode,
                SubjectCode: subjectCode.ToUpperInvariant(),
                SubjectName: FlmValueParser.Truncate(name, CatalogRules.CourseNameMaxLength)!,
                TermNo: termNo.Value,
                Credits: FlmValueParser.ParseIntOrNull(Cell(row, 8)) ?? 0,
                CountsTowardGpa: FlmValueParser.ParseCountsGpa(Cell(row, 9)),
                PrerequisiteText: null,
                Description: null,
                SyllabusCode: FlmValueParser.Truncate(Cell(row, 13), 20),
                MinAvgMarkToPass: null);
        }
    }

    /// <summary>
    /// The BangDiem sheet. Columns:
    /// Ngành/lớp | Mã môn | Cấp | Category | Type | Part | Weight |
    /// Completion Criteria | Duration | ...
    ///
    /// "Cấp" is "Chính" for a top-level component and "↳ Chi tiết" for a
    /// sub-row; only the top-level ones count toward the 100% total.
    /// </summary>
    private static IReadOnlyList<FlmAssessmentRow> ReadGradeStructure(IXLWorkbook workbook)
    {
        if (!workbook.TryGetWorksheet(GradeStructureSheet, out var sheet))
        {
            return [];
        }

        var result = new List<FlmAssessmentRow>();

        // The same syllabus repeats once per programme that teaches it; keeping
        // only the first (subject, category) is what keeps the import idempotent
        // against UQ_Assessment_Name.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var orderPerSubject = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() >= FlatSheetFirstDataRow))
        {
            var subjectCode = FlmValueParser.Clean(Cell(row, 2))?.ToUpperInvariant();
            var category = FlmValueParser.Truncate(Cell(row, 4), CatalogRules.AssessmentNameMaxLength);

            if (subjectCode is null || category is null || !seen.Add($"{subjectCode}|{category}"))
            {
                continue;
            }

            orderPerSubject.TryGetValue(subjectCode, out var order);
            orderPerSubject[subjectCode] = order + 1;

            var level = FlmValueParser.Clean(Cell(row, 3));

            result.Add(new FlmAssessmentRow(
                SubjectCode: subjectCode,
                Category: category,
                Type: FlmValueParser.Truncate(Cell(row, 5), 100),
                WeightPercent: FlmValueParser.ParsePercent(Cell(row, 7)),
                CompletionCriteria: FlmValueParser.Clean(Cell(row, 8)),
                IsSubComponent: level is not null && !level.Equals("Chính", StringComparison.OrdinalIgnoreCase),
                DisplayOrder: order,
                PartCount: FlmValueParser.ParseIntOrNull(Cell(row, 6)) ?? 1));
        }

        return result;
    }

    /// <summary>
    /// The TaiLieu sheet. Columns:
    /// Ngành/lớp | Mã môn | Loại | Tiêu đề | Link | Tác giả | NXB | ISBN | Ghi chú
    /// </summary>
    private static IReadOnlyList<FlmMaterialRow> ReadMaterials(IXLWorkbook workbook)
    {
        if (!workbook.TryGetWorksheet(MaterialsSheet, out var sheet))
        {
            return [];
        }

        var result = new List<FlmMaterialRow>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var orderPerSubject = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() >= FlatSheetFirstDataRow))
        {
            var subjectCode = FlmValueParser.Clean(Cell(row, 2))?.ToUpperInvariant();
            var title = FlmValueParser.Truncate(Cell(row, 4), CatalogRules.MaterialTitleMaxLength);

            if (subjectCode is null || title is null || !seen.Add($"{subjectCode}|{title}"))
            {
                continue;
            }

            orderPerSubject.TryGetValue(subjectCode, out var order);
            orderPerSubject[subjectCode] = order + 1;

            result.Add(new FlmMaterialRow(
                SubjectCode: subjectCode,
                Title: title,
                Url: FlmValueParser.Truncate(Cell(row, 5), CatalogRules.UrlMaxLength),
                Author: FlmValueParser.Truncate(Cell(row, 6), 200),
                Publisher: FlmValueParser.Truncate(Cell(row, 7), 200),
                Isbn: FlmValueParser.Truncate(Cell(row, 8), 50),
                Note: FlmValueParser.Clean(Cell(row, 9)),
                DisplayOrder: order));
        }

        return result;
    }

    /// <summary>
    /// The LichKiemTra sheet. Columns:
    /// Ngành/lớp | Mã môn | Buổi | Nội dung / bài kiểm tra | Hình thức | LO
    /// </summary>
    private static IReadOnlyList<FlmScheduleRow> ReadSchedule(IXLWorkbook workbook)
    {
        if (!workbook.TryGetWorksheet(ScheduleSheet, out var sheet))
        {
            return [];
        }

        var result = new List<FlmScheduleRow>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() >= FlatSheetFirstDataRow))
        {
            var subjectCode = FlmValueParser.Clean(Cell(row, 2))?.ToUpperInvariant();
            var sessionNo = FlmValueParser.ParseIntOrNull(Cell(row, 3));
            var title = FlmValueParser.Truncate(Cell(row, 4), CatalogRules.MaterialTitleMaxLength);

            if (subjectCode is null || sessionNo is null || sessionNo < 1 || title is null)
            {
                continue;
            }

            if (!seen.Add($"{subjectCode}|{sessionNo}"))
            {
                continue;
            }

            result.Add(new FlmScheduleRow(
                SubjectCode: subjectCode,
                SessionNo: sessionNo.Value,
                Title: title,
                TeachingType: FlmValueParser.Truncate(Cell(row, 5), 100)));
        }

        return result;
    }

    /// <summary>
    /// Reads a cell as text.
    ///
    /// GetString(), not Value.ToString(): a numeric cell would otherwise come
    /// back as "2446.0" and every code comparison against it would fail.
    /// </summary>
    private static string? Cell(IXLRow row, int column)
        => row.Cell(column).GetString();
}
