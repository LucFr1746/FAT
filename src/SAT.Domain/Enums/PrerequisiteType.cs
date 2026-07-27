namespace SAT.Domain.Enums;

/// <summary>Kiểu ràng buộc giữa hai môn học.</summary>
public enum PrerequisiteType
{
    /// <summary>Phải ĐẠT môn kia ở một kỳ TRƯỚC mới được đăng ký.</summary>
    Prerequisite = 0,

    /// <summary>Được học CÙNG kỳ, chỉ cần đăng ký song song.</summary>
    Corequisite = 1
}
