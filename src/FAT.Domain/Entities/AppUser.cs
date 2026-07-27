namespace FAT.Domain.Entities;

/// <summary>
/// A login account. Maps to dbo.AppUser - deliberately not named "User",
/// because USER is a reserved word in T-SQL.
/// </summary>
public class AppUser
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// BCrypt hash. Nullable for Google OAuth only accounts.
    /// Only AuthService should ever touch this field; it must
    /// never reach a view model and never appear in a log.
    /// </summary>
    public string? PasswordHash { get; set; }

    public int RoleId { get; set; }
    public string? GoogleId { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public Role? Role { get; set; }

    /// <summary>Null for Admin accounts, which have no student profile.</summary>
    public Student? Student { get; set; }
}

