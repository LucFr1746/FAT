namespace Domain.Entities;

/// <summary>An audit trail entry for data changes, mostly by administrators.</summary>
public class AuditLog
{
    public long AuditLogId { get; set; }

    /// <summary>
    /// Nullable on purpose: deleting an account must NOT erase the record of
    /// what that account did.
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
