namespace SAT.Domain.Entities;

/// <summary>
/// Một dòng trong khung chương trình đào tạo: ngành X, kỳ thứ N phải học môn Y.
/// Đây là chuẩn để đối chiếu ra tiến độ tốt nghiệp.
/// </summary>
public class Curriculum
{
    public int CurriculumId { get; set; }
    public int MajorId { get; set; }
    public int CourseId { get; set; }

    /// <summary>Kỳ thứ mấy trong lộ trình chuẩn (1-based).</summary>
    public int TermNo { get; set; }

    public bool IsMandatory { get; set; } = true;

    public Major? Major { get; set; }
    public Course? Course { get; set; }
}
