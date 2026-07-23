using FAT.Services.Dtos;

namespace FAT.Services.Implementations;

/// <summary>
/// Tallies one import run.
///
/// Created and Updated are tracked separately because that split is the proof of
/// idempotency: a second run of the same file must report Created = 0. A single
/// "rows affected" number would hide a duplicate-insert bug completely.
/// </summary>
internal sealed class ImportSession
{
    public ImportSession(IReadOnlyList<string> initialWarnings)
    {
        Warnings.AddRange(initialWarnings);
    }

    public ImportCounter Majors { get; } = new();
    public ImportCounter Subjects { get; } = new();
    public ImportCounter CurriculumLinks { get; } = new();
    public ImportCounter Prerequisites { get; } = new();
    public ImportCounter Assessments { get; } = new();
    public ImportCounter Materials { get; } = new();
    public ImportCounter Schedules { get; } = new();

    public List<string> Warnings { get; } = [];

    /// <summary>
    /// Adds a warning, but stops well short of unbounded growth.
    ///
    /// A malformed file can produce thousands of near-identical warnings; a list
    /// that long is unreadable in the UI and pointless to keep in memory.
    /// </summary>
    public void Warn(string message)
    {
        const int maxWarnings = 100;

        if (Warnings.Count < maxWarnings)
        {
            Warnings.Add(message);
        }
        else if (Warnings.Count == maxWarnings)
        {
            Warnings.Add("... (còn nhiều cảnh báo khác đã được lược bớt)");
        }
    }

    public ImportResultDto ToResult(TimeSpan duration) => new(
        IsSuccess: true,
        Majors: Majors.ToDto(),
        Subjects: Subjects.ToDto(),
        CurriculumLinks: CurriculumLinks.ToDto(),
        Prerequisites: Prerequisites.ToDto(),
        Assessments: Assessments.ToDto(),
        Materials: Materials.ToDto(),
        Schedules: Schedules.ToDto(),
        Warnings: Warnings,
        Errors: [],
        Duration: duration);
}

/// <summary>Mutable created/updated/skipped tally for one kind of row.</summary>
internal sealed class ImportCounter
{
    public int Created { get; private set; }
    public int Updated { get; private set; }
    public int Skipped { get; private set; }

    public void CountCreated() => Created++;
    public void CountUpdated() => Updated++;
    public void CountSkipped() => Skipped++;

    /// <summary>Records the outcome of an upsert: changed rows count as updated.</summary>
    public void CountUpsert(bool isNew, bool wasModified)
    {
        if (isNew)
        {
            Created++;
        }
        else if (wasModified)
        {
            Updated++;
        }
        else
        {
            Skipped++;
        }
    }

    public ImportCounts ToDto() => new(Created, Updated, Skipped);
}
