namespace SAT.Domain.Entities;

/// <summary>Ghi vết thao tác thay đổi dữ liệu, chủ yếu của Admin.</summary>
public class AuditLog
{
    public long AuditLogId { get; set; }

    /// <summary>
    /// Null được: xóa tài khoản KHÔNG được làm mất dấu vết thao tác của
    /// tài khoản đó.
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>Create | Update | Delete | Login | Logout ...</summary>
    public string Action { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Detail { get; set; }
    public DateTime CreatedAt { get; set; }

    public AppUser? User { get; set; }
}
