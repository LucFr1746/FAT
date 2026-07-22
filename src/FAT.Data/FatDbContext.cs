using FAT.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FAT.Data;

/// <summary>
/// The application's EF Core context.
///
/// OWNED BY THE TEAM LEAD. The schema is frozen - see docs/TEAM.md.
///
/// This project does NOT use EF Core Migrations: the scripts under db/ are the
/// source of truth for the schema. The reason is that five people working in
/// parallel who each generate migrations will break the snapshot chain, and
/// untangling that costs an afternoon nobody has.
/// To change a column: edit db/01_schema.sql and the matching entity, then tell
/// the team to re-run db/setup-db.ps1.
///
/// Because the context does not create tables, it IS the unit of work already -
/// this project deliberately does not wrap another UnitOfWork around it.
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Pick up every IEntityTypeConfiguration<> in this assembly.
        // Adding a new configuration is just adding a class - no edit here.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FatDbContext).Assembly);
    }
}
