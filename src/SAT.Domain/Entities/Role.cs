namespace SAT.Domain.Entities;

/// <summary>Vai trò đăng nhập: Admin hoặc Student.</summary>
public class Role
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<AppUser> Users { get; set; } = new List<AppUser>();
}
