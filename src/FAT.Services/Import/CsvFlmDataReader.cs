using FAT.Domain.Constants;

namespace FAT.Services.Import;

/// <summary>
/// Reads the FLM export from the CSV folder (db/data/csv).
///
/// Accepts either the folder itself or any .csv inside it, because a file
/// picker naturally lands on a file while the caller naturally has the folder.
/// </summary>
public sealed class CsvFlmDataReader : IFlmDataReader
{
    private const string CurriculaFile = "curricula.csv";
    private const string SubjectsFile = "subjects.csv";
    private const string AssessmentsFile = "assessments.csv";
    private const string MaterialsFile = "materials.csv";
    private const string FilesFile = "files.csv";
    private const string SessionsFile = "sessions.csv";
    private const string CombosFile = "combos.csv";

    /// <summary>
    /// The placeholder subjects.csv uses for elective subjects that belong to no
    /// single programme. Their real placement lives in combos.csv.
    /// </summary>
    private const string ComboPlaceholder = "(combo)";

    public string SourceName => "CSV folder";

    public bool CanRead(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var folder = ResolveFolder(path);
        return folder is not null && File.Exists(Path.Combine(folder, SubjectsFile));
    }

    public async Task<FlmDataSet> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        var folder = ResolveFolder(path)
            ?? throw new DirectoryNotFoundException($"Không tìm thấy thư mục dữ liệu CSV: {path}");

        var subjectsPath = Path.Combine(folder, SubjectsFile);
        if (!File.Exists(subjectsPath))
        {
            throw new FileNotFoundException(
                $"Thiếu tệp bắt buộc '{SubjectsFile}' trong thư mục {folder}.", subjectsPath);
        }

        var curricula = await ReadCurriculaAsync(folder, cancellationToken);
        var subjectRows = await CsvFile.ReadAsync(subjectsPath, cancellationToken);

        var subjects = new List<FlmSubjectRow>();
        subjects.AddRange(ReadSubjects(subjectRows));

        // Elective subjects sit in subjects.csv under the "(combo)" placeholder
        // with no kỳ, so the pass above cannot place them. combos.csv holds the
        // real programme and kỳ for each one. Skipping this file would silently
        // drop 48 subjects - which is exactly the gap that made the CSV and
        // Excel readers disagree.
        subjects.AddRange(await ReadCombosAsync(folder, subjectRows, cancellationToken));

        var assessments = await ReadAssessmentsAsync(folder, cancellationToken);
        var materials = await ReadMaterialsAsync(folder, cancellationToken);
        var schedules = await ReadSchedulesAsync(folder, cancellationToken);

        return new FlmDataSet(curricula, subjects, assessments, materials, schedules);
    }

    /// <summary>Accepts a folder, or a file inside the folder, and returns the folder.</summary>
    private static string? ResolveFolder(string path)
    {
        if (Directory.Exists(path))
        {
            return path;
        }

        if (File.Exists(path) &&
            string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetDirectoryName(path);
        }

        return null;
    }

    private static async Task<IReadOnlyList<FlmCurriculumRow>> ReadCurriculaAsync(
        string folder, CancellationToken cancellationToken)
    {
        var rows = await CsvFile.ReadAsync(Path.Combine(folder, CurriculaFile), cancellationToken);

        return rows
            .Select(r => FlmValueParser.Clean(Get(r, "curriculum")))
            .Where(code => code is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(code => new FlmCurriculumRow(code!, null))
            .ToList();
    }

    /// <summary>Subjects that subjects.csv already places into a programme and kỳ.</summary>
    private static IEnumerable<FlmSubjectRow> ReadSubjects(
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows)
    {
        foreach (var row in rows)
        {
            var majorCode = FlmValueParser.Clean(Get(row, "curriculum"));
            var subjectCode = FlmValueParser.Clean(Get(row, "subjectCode"));

            if (majorCode is null || subjectCode is null ||
                majorCode.Equals(ComboPlaceholder, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // A subject with no kỳ is unplaced in that curriculum and cannot
            // become a curriculum row.
            var termNo = FlmValueParser.ParseIntOrNull(Get(row, "semester"));
            if (termNo is null)
            {
                continue;
            }

            yield return ToSubjectRow(row, majorCode, subjectCode, termNo.Value);
        }
    }

    /// <summary>
    /// Elective subjects, placed by combos.csv.
    ///
    /// combos.csv carries the programme, kỳ, credits and GPA flag but no name or
    /// description, so those are looked up from the "(combo)" rows in
    /// subjects.csv.
    /// </summary>
    private static async Task<IReadOnlyList<FlmSubjectRow>> ReadCombosAsync(
        string folder,
        IReadOnlyList<IReadOnlyDictionary<string, string>> subjectRows,
        CancellationToken cancellationToken)
    {
        var comboRows = await CsvFile.ReadAsync(Path.Combine(folder, CombosFile), cancellationToken);
        if (comboRows.Count == 0)
        {
            return [];
        }

        // Details keyed by subject code, preferring a row that actually has a
        // description over a bare placeholder.
        var detailsByCode = subjectRows
            .Where(r => FlmValueParser.Clean(Get(r, "subjectCode")) is not null)
            .GroupBy(r => FlmValueParser.Clean(Get(r, "subjectCode"))!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(r => FlmValueParser.Clean(Get(r, "description")) is not null).First(),
                StringComparer.OrdinalIgnoreCase);

        var result = new List<FlmSubjectRow>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in comboRows)
        {
            var majorCode = FlmValueParser.Clean(Get(row, "curriculum"));
            var subjectCode = FlmValueParser.Clean(Get(row, "subjectCode"));
            var termNo = FlmValueParser.ParseIntOrNull(Get(row, "semester"));

            if (majorCode is null || subjectCode is null || termNo is null)
            {
                continue;
            }

            // The same elective is listed once per combo slot that offers it;
            // one curriculum link per programme is all the catalog can hold.
            if (!seen.Add($"{majorCode}|{subjectCode}"))
            {
                continue;
            }

            detailsByCode.TryGetValue(subjectCode, out var details);

            result.Add(new FlmSubjectRow(
                MajorCode: majorCode,
                SubjectCode: subjectCode.ToUpperInvariant(),
                SubjectName: FlmValueParser.Truncate(
                                 details is null ? null : Get(details, "syllabusName"),
                                 CatalogRules.CourseNameMaxLength)
                             ?? subjectCode,
                TermNo: termNo.Value,
                // combos.csv is the authority on credits here; subjects.csv
                // repeats them but the combo listing is what the programme uses.
                Credits: FlmValueParser.ParseIntOrNull(Get(row, "noCredit")) ?? 0,
                CountsTowardGpa: FlmValueParser.ParseCountsGpa(Get(row, "countsGPA")),
                PrerequisiteText: FlmValueParser.Truncate(
                    details is null ? null : Get(details, "preRequisite"), CatalogRules.DescriptionMaxLength),
                Description: FlmValueParser.Clean(details is null ? null : Get(details, "description")),
                SyllabusCode: FlmValueParser.Truncate(Get(row, "sylid"), 20),
                MinAvgMarkToPass: FlmValueParser.ParseCompletionCriteria(
                    details is null ? null : Get(details, "minAvgMarkToPass"))));
        }

        return result;
    }

    private static FlmSubjectRow ToSubjectRow(
        IReadOnlyDictionary<string, string> row, string majorCode, string subjectCode, int termNo)
        => new(
            MajorCode: majorCode,
            SubjectCode: subjectCode.ToUpperInvariant(),
            SubjectName: FlmValueParser.Truncate(Get(row, "syllabusName"), CatalogRules.CourseNameMaxLength)
                         ?? subjectCode,
            TermNo: termNo,
            Credits: FlmValueParser.ParseIntOrNull(Get(row, "noCredit")) ?? 0,
            CountsTowardGpa: FlmValueParser.ParseCountsGpa(Get(row, "countsGPA")),
            PrerequisiteText: FlmValueParser.Truncate(Get(row, "preRequisite"), CatalogRules.DescriptionMaxLength),
            Description: FlmValueParser.Clean(Get(row, "description")),
            SyllabusCode: FlmValueParser.Truncate(Get(row, "sylid"), 20),
            MinAvgMarkToPass: FlmValueParser.ParseCompletionCriteria(Get(row, "minAvgMarkToPass")));

    private static async Task<IReadOnlyList<FlmAssessmentRow>> ReadAssessmentsAsync(
        string folder, CancellationToken cancellationToken)
    {
        var rows = await CsvFile.ReadAsync(Path.Combine(folder, AssessmentsFile), cancellationToken);
        var result = new List<FlmAssessmentRow>();

        // The same syllabus is repeated once per curriculum that teaches it.
        // Keeping only the first occurrence per (subject, category) is what
        // makes the import idempotent against UQ_Assessment_Name.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var orderPerSubject = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var subjectCode = FlmValueParser.Clean(Get(row, "subjectCode"))?.ToUpperInvariant();
            var category = FlmValueParser.Truncate(Get(row, "category"), CatalogRules.AssessmentNameMaxLength);

            if (subjectCode is null || category is null)
            {
                continue;
            }

            if (!seen.Add($"{subjectCode}|{category}"))
            {
                continue;
            }

            orderPerSubject.TryGetValue(subjectCode, out var order);
            orderPerSubject[subjectCode] = order + 1;

            result.Add(new FlmAssessmentRow(
                SubjectCode: subjectCode,
                Category: category,
                Type: FlmValueParser.Truncate(Get(row, "type"), 100),
                WeightPercent: FlmValueParser.ParsePercent(Get(row, "weight")),
                CompletionCriteria: FlmValueParser.Clean(Get(row, "completionCriteria")),
                IsSubComponent: Get(row, "isSubComponent")?.Trim() == "1",
                DisplayOrder: order));
        }

        return result;
    }

    /// <summary>
    /// Merges the two material sources: materials.csv holds bibliographic
    /// entries and files.csv holds downloadable links. The workbook presents
    /// them as one "TaiLieu" sheet, so they must be merged here too.
    /// </summary>
    private static async Task<IReadOnlyList<FlmMaterialRow>> ReadMaterialsAsync(
        string folder, CancellationToken cancellationToken)
    {
        var result = new List<FlmMaterialRow>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var orderPerSubject = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        void Add(string subjectCode, string title, string? url, string? author, string? publisher,
                 string? isbn, string? note)
        {
            if (!seen.Add($"{subjectCode}|{title}"))
            {
                return;
            }

            orderPerSubject.TryGetValue(subjectCode, out var order);
            orderPerSubject[subjectCode] = order + 1;

            result.Add(new FlmMaterialRow(subjectCode, title, url, author, publisher, isbn, note, order));
        }

        foreach (var row in await CsvFile.ReadAsync(Path.Combine(folder, MaterialsFile), cancellationToken))
        {
            var subjectCode = FlmValueParser.Clean(Get(row, "subjectCode"))?.ToUpperInvariant();
            var title = FlmValueParser.Truncate(Get(row, "materialDescription"), CatalogRules.MaterialTitleMaxLength);

            if (subjectCode is not null && title is not null)
            {
                Add(subjectCode, title,
                    url: null,
                    author: FlmValueParser.Truncate(Get(row, "author"), 200),
                    publisher: FlmValueParser.Truncate(Get(row, "publisher"), 200),
                    isbn: FlmValueParser.Truncate(Get(row, "isbn"), 50),
                    note: FlmValueParser.Clean(Get(row, "note")));
            }
        }

        foreach (var row in await CsvFile.ReadAsync(Path.Combine(folder, FilesFile), cancellationToken))
        {
            var subjectCode = FlmValueParser.Clean(Get(row, "subjectCode"))?.ToUpperInvariant();
            var title = FlmValueParser.Truncate(Get(row, "text"), CatalogRules.MaterialTitleMaxLength);

            if (subjectCode is not null && title is not null)
            {
                Add(subjectCode, title,
                    url: FlmValueParser.Truncate(Get(row, "href"), CatalogRules.UrlMaxLength),
                    author: null, publisher: null, isbn: null, note: null);
            }
        }

        return result;
    }

    private static async Task<IReadOnlyList<FlmScheduleRow>> ReadSchedulesAsync(
        string folder, CancellationToken cancellationToken)
    {
        var rows = await CsvFile.ReadAsync(Path.Combine(folder, SessionsFile), cancellationToken);
        var result = new List<FlmScheduleRow>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var subjectCode = FlmValueParser.Clean(Get(row, "subjectCode"))?.ToUpperInvariant();
            var sessionNo = FlmValueParser.ParseIntOrNull(Get(row, "session"));
            var topic = FlmValueParser.Clean(Get(row, "topic"));

            if (subjectCode is null || sessionNo is null || sessionNo < 1 || topic is null)
            {
                continue;
            }

            // Only graded sessions, matching what the workbook's LichKiemTra
            // sheet already contains.
            if (!AssessmentSessionFilter.IsAssessmentSession(topic))
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
                Title: FlmValueParser.Truncate(topic, CatalogRules.MaterialTitleMaxLength)!,
                TeachingType: FlmValueParser.Truncate(Get(row, "teachingType"), 100)));
        }

        return result;
    }

    private static string? Get(IReadOnlyDictionary<string, string> row, string column)
        => row.TryGetValue(column, out var value) ? value : null;
}
