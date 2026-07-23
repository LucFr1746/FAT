namespace FAT.Services.Dtos;

/// <summary>What an import found in the file, before anything is written.</summary>
public sealed record ImportPreviewDto(
    string SourceName,
    string FilePath,
    int MajorCount,
    int SubjectCount,
    int CurriculumLinkCount,
    int AssessmentCount,
    int MaterialCount,
    int ScheduleCount,
    IReadOnlyList<string> Warnings)
{
    public bool HasData => SubjectCount > 0;
}

/// <summary>
/// What an import actually did.
///
/// Created and Updated are reported separately on purpose: re-running an import
/// must show Created = 0, and that number is the quickest proof that the upsert
/// is not quietly duplicating rows.
/// </summary>
public sealed record ImportResultDto(
    bool IsSuccess,
    ImportCounts Majors,
    ImportCounts Subjects,
    ImportCounts CurriculumLinks,
    ImportCounts Prerequisites,
    ImportCounts Assessments,
    ImportCounts Materials,
    ImportCounts Schedules,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors,
    TimeSpan Duration)
{
    public int TotalCreated =>
        Majors.Created + Subjects.Created + CurriculumLinks.Created + Prerequisites.Created +
        Assessments.Created + Materials.Created + Schedules.Created;

    public int TotalUpdated =>
        Majors.Updated + Subjects.Updated + CurriculumLinks.Updated + Prerequisites.Updated +
        Assessments.Updated + Materials.Updated + Schedules.Updated;

    public static ImportResultDto Failure(string error, TimeSpan duration) => new(
        false,
        ImportCounts.Zero, ImportCounts.Zero, ImportCounts.Zero, ImportCounts.Zero,
        ImportCounts.Zero, ImportCounts.Zero, ImportCounts.Zero,
        [], [error], duration);
}

/// <summary>Created / updated / skipped totals for one kind of row.</summary>
public sealed record ImportCounts(int Created, int Updated, int Skipped)
{
    public static ImportCounts Zero { get; } = new(0, 0, 0);

    public int Total => Created + Updated + Skipped;
}

/// <summary>Knobs on an import run.</summary>
public sealed record ImportOptions(
    /// <summary>
    /// When false, existing rows are left untouched and only new ones are
    /// inserted. Useful once an administrator has hand-edited the catalog and
    /// does not want a re-import to overwrite their corrections.
    /// </summary>
    bool UpdateExisting = true,

    /// <summary>Import the syllabus bibliography.</summary>
    bool ImportMaterials = true,

    /// <summary>Import the grade structure.</summary>
    bool ImportAssessments = true,

    /// <summary>Import the assessment timeline.</summary>
    bool ImportSchedules = true,

    /// <summary>Parse the prerequisite text into Prerequisite rows.</summary>
    bool ImportPrerequisites = true)
{
    public static ImportOptions Default { get; } = new();
}
