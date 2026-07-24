namespace FAT.Services.Import;

/// <summary>
/// The FLM export, normalised into one shape.
///
/// Both the .xlsx reader and the .json reader produce THIS, so everything
/// downstream - validation, the prerequisite parser, the upsert logic - is
/// written once and is identical whichever source the administrator picked.
/// </summary>
public sealed record FlmDataSet(
    IReadOnlyList<FlmCurriculumRow> Curricula,
    IReadOnlyList<FlmSubjectRow> Subjects,
    IReadOnlyList<FlmAssessmentRow> Assessments,
    IReadOnlyList<FlmMaterialRow> Materials,
    IReadOnlyList<FlmScheduleRow> Schedules)
{
    public static FlmDataSet Empty { get; } = new([], [], [], [], []);

    /// <summary>Rows read in total - what the preview screen reports.</summary>
    public int TotalRows =>
        Curricula.Count + Subjects.Count + Assessments.Count + Materials.Count + Schedules.Count;
}

/// <summary>One degree programme - a sheet in the workbook, a row in curricula.json.</summary>
public sealed record FlmCurriculumRow(string MajorCode, string? MajorName);

/// <summary>
/// One subject AS PLACED IN ONE CURRICULUM.
///
/// The same <see cref="SubjectCode"/> legitimately appears once per major, and
/// 16 of them sit in a DIFFERENT <see cref="TermNo"/> depending on the major -
/// which is why the term belongs on the curriculum link and never on the
/// subject itself.
/// </summary>
public sealed record FlmSubjectRow(
    string MajorCode,
    string SubjectCode,
    string SubjectName,
    int TermNo,
    int Credits,
    bool CountsTowardGpa,
    string? PrerequisiteText,
    string? Description,
    string? SyllabusCode,
    decimal? MinAvgMarkToPass);

/// <summary>
/// One grade component. Keyed by subject, not by curriculum: every subject code
/// in the FLM data has exactly one syllabus, so the structure is the same
/// wherever the subject is taught.
/// </summary>
public sealed record FlmAssessmentRow(
    string SubjectCode,
    string Category,
    string? Type,
    decimal WeightPercent,
    string? CompletionCriteria,
    bool IsSubComponent,
    int DisplayOrder);

/// <summary>One reading or link from the syllabus bibliography.</summary>
public sealed record FlmMaterialRow(
    string SubjectCode,
    string Title,
    string? Url,
    string? Author,
    string? Publisher,
    string? Isbn,
    string? Note,
    int DisplayOrder);

/// <summary>One assessment-bearing session from the syllabus timeline.</summary>
public sealed record FlmScheduleRow(
    string SubjectCode,
    int SessionNo,
    string Title,
    string? TeachingType);
