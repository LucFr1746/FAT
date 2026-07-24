namespace Domain.Enums;

/// <summary>Kind of dependency between two courses.</summary>
public enum PrerequisiteType
{
    /// <summary>The other course must be PASSED in an EARLIER term.</summary>
    Prerequisite = 0,

    /// <summary>The other course may be taken in the SAME term.</summary>
    Corequisite = 1
}
