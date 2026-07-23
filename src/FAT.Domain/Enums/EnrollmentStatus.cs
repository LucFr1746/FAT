namespace FAT.Domain.Enums;

/// <summary>
/// State of a single attempt at a course by a student.
/// Persisted as a STRING (see EnrollmentConfiguration) so that reading the
/// table in SSMS shows 'Passed' rather than a meaningless 0/1/2.
/// </summary>
public enum EnrollmentStatus
{
    /// <summary>In progress; the final score has not been settled yet.</summary>
    Studying = 0,

    /// <summary>Passed. Only this state contributes to the GPA.</summary>
    Passed = 1,

    /// <summary>Failed: final score below 5.0, or a component below its minimum.</summary>
    Failed = 2,

    /// <summary>Withdrawn mid-term. Counts toward neither GPA nor credits.</summary>
    Withdrawn = 3
}
