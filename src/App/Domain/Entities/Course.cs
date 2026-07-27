namespace Domain.Entities;

/// <summary>A course in the catalog.</summary>
public class Course
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int Credits { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether this subject's score enters the GPA at all.
    ///
    /// False for the ones FPT marks "Tính GPA = Không" - physical education,
    /// the orientation programme, the political-theory block. They still carry
    /// credits and still have to be PASSED to graduate, but including their
    /// scores would distort the GPA.
    /// </summary>
    public bool CountsTowardGpa { get; set; } = true;

    /// <summary>
    /// Subject-specific pass mark, when it differs from AcademicRules.PassScore.
    /// Null means the standard 5.0 applies.
    /// </summary>
    public decimal? MinAvgMarkToPass { get; set; }

    /// <summary>FLM syllabus id ("sylid"), kept so an import can be traced back to its source row.</summary>
    public string? SyllabusCode { get; set; }

    /// <summary>
    /// The prerequisite exactly as FLM words it.
    ///
    /// Kept alongside the parsed <see cref="Prerequisites"/> rows because some
    /// requirements are prose that no parser can turn into course ids - for
    /// example "Sinh viên đạt 90% tổng số tín chỉ trước kỳ OJT". Dropping the
    /// text would silently lose the rule; showing it lets a human apply it.
    /// </summary>
    public string? PrerequisiteText { get; set; }

    /// <summary>Courses that THIS course requires to be completed first.</summary>
    public ICollection<Prerequisite> Prerequisites { get; set; } = new List<Prerequisite>();

    /// <summary>Courses that list THIS course as their prerequisite (reverse direction).</summary>
    public ICollection<Prerequisite> RequiredFor { get; set; } = new List<Prerequisite>();

    public ICollection<Assessment> Assessments { get; set; } = new List<Assessment>();
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<Curriculum> CurriculumItems { get; set; } = new List<Curriculum>();
    public ICollection<SubjectMaterial> SubjectMaterials { get; set; } = new List<SubjectMaterial>();
    public ICollection<AssessmentSchedule> AssessmentSchedules { get; set; } = new List<AssessmentSchedule>();
}
