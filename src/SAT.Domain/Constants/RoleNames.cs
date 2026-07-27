namespace SAT.Domain.Constants;

/// <summary>
/// Tên vai trò, phải khớp CHÍNH XÁC với cột Role.RoleName trong 02_seed_master.sql.
/// Dùng hằng số thay vì gõ chuỗi "Admin" rải rác: gõ sai chuỗi thì trình biên
/// dịch không bắt được, và hậu quả là phân quyền âm thầm cho qua.
/// </summary>
public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Student = "Student";
}
