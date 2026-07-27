namespace SAT.Domain.Entities;

/// <summary>
/// Nội dung nhị phân của tài liệu, tách khỏi <see cref="Material"/>.
/// Quan hệ 1-1, dùng chung khóa chính MaterialId.
/// </summary>
public class MaterialFile
{
    public int MaterialId { get; set; }

    public byte[] Content { get; set; } = [];

    public Material? Material { get; set; }
}
