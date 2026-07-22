namespace SAT.Domain.Entities;

/// <summary>Ngành đào tạo.</summary>
public class Major
{
    public int MajorId { get; set; }
    public string MajorCode { get; set; } = string.Empty;
    public string MajorName { get; set; } = string.Empty;

    /// <summary>
    /// Tổng tín chỉ cần để tốt nghiệp. Là MẪU SỐ khi tính % tiến độ tốt nghiệp,
    /// nên phải luôn khớp tổng tín chỉ trong Curriculum của ngành.
    /// </summary>
    public int RequiredCredits { get; set; }

    public int TotalTerms { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Student> Students { get; set; } = new List<Student>();
    public ICollection<Curriculum> CurriculumItems { get; set; } = new List<Curriculum>();
}
