namespace SAT.Domain.Entities;

/// <summary>
/// Tài khoản đăng nhập. Khớp bảng dbo.AppUser (không đặt tên "User" vì đó là
/// từ khóa dành riêng của T-SQL).
/// </summary>
public class AppUser
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Hash BCrypt. Chỉ AuthService được đụng tới trường này; không bao giờ
    /// đưa lên ViewModel hay ghi ra log.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    public int RoleId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public Role? Role { get; set; }

    /// <summary>Null với tài khoản Admin (Admin không phải sinh viên).</summary>
    public Student? Student { get; set; }
}
