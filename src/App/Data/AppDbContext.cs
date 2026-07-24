using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Data;

/// <summary>
/// The application's EF Core context.
/// </summary>
public class FAT_DBContext : DbContext
{
    public FAT_DBContext(DbContextOptions<FAT_DBContext> options) : base(options)
    {
    }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Major> Majors => Set<Major>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Prerequisite> Prerequisites => Set<Prerequisite>();
    public DbSet<Curriculum> CurriculumItems => Set<Curriculum>();
    public DbSet<Semester> Semesters => Set<Semester>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<GradeScale> GradeScales => Set<GradeScale>();
    public DbSet<AcademicPlan> AcademicPlans => Set<AcademicPlan>();
    public DbSet<AcademicPlanItem> AcademicPlanItems => Set<AcademicPlanItem>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<MaterialFile> MaterialFiles => Set<MaterialFile>();
    public DbSet<Term> Terms => Set<Term>();
    public DbSet<SubjectMaterial> SubjectMaterials => Set<SubjectMaterial>();
    public DbSet<AssessmentSchedule> AssessmentSchedules => Set<AssessmentSchedule>();
    public DbSet<GradePrediction> GradePredictions => Set<GradePrediction>();

    /// <summary>
    /// Brings an existing database up to the current model. The individual
    /// changes live in <see cref="SchemaUpgrader"/>; this method only decides
    /// whether a failure should stop the caller.
    ///
    /// Swallowing the exception is deliberate: the app must still start against
    /// an in-memory provider or a database the user cannot ALTER. When the
    /// schema really is stale the first query fails with a message that names
    /// the missing column, which is more useful than a crash on launch.
    /// </summary>
    public void EnsureDatabaseSchemaUpToDate()
    {
        try
        {
            SchemaUpgrader.Upgrade(this);
        }
        catch
        {
            // Non-relational provider, or an account without ALTER rights.
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Pick up every IEntityTypeConfiguration<> in this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FAT_DBContext).Assembly);
    }
}
