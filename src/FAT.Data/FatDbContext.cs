using FAT.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FAT.Data;

/// <summary>
/// The application's EF Core context.
/// </summary>
public class FatDbContext : DbContext
{
    public FatDbContext(DbContextOptions<FatDbContext> options) : base(options)
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

    public void EnsureDatabaseSchemaUpToDate()
    {
        try
        {
            if (Database.IsSqlServer())
            {
                Database.ExecuteSqlRaw(@"
                    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Student')
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Student') AND name = 'CurrentSemester')
                        BEGIN
                            ALTER TABLE dbo.Student ADD CurrentSemester NVARCHAR(20) NULL;
                        END
                    END
                ");
            }
        }
        catch
        {
            // Ignore for in-memory or non-relational test providers
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Pick up every IEntityTypeConfiguration<> in this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FatDbContext).Assembly);
    }
}
