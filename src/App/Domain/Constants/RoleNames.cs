namespace Domain.Constants;

/// <summary>
/// Role names. These must match the Role.RoleName column seeded by
/// db/02_seed_master.sql EXACTLY.
///
/// Using constants instead of typing "Admin" in a dozen places matters here:
/// a typo in a literal compiles fine, and the consequence is an authorization
/// check that silently lets everything through.
/// </summary>
public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Student = "Student";
}
