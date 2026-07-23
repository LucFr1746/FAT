using Microsoft.EntityFrameworkCore;
using SAT.Domain.Entities;

namespace SAT.Data;

/// <summary>
/// DbContext của toàn ứng dụng.
///
/// 🔒 CHỈ TV1 (Lead) ĐƯỢC SỬA FILE NÀY. Schema đã đóng băng cuối Day 1.
///
/// Dự án KHÔNG dùng EF Core Migrations: nguồn sự thật của schema là các file
/// trong thư mục db/. Lý do là 5 người chạy song song mà cùng sinh migration
/// thì chuỗi snapshot vỡ và mất cả buổi để gỡ (xem docs/plan §1).
/// Muốn đổi cột: sửa db/01_schema.sql + entity tương ứng, rồi báo cả nhóm
/// chạy lại db/setup-db.ps1.
///
/// Vì DbContext không tạo bảng, nó chính là Unit of Work sẵn có - dự án KHÔNG
/// bọc thêm một lớp UnitOfWork nữa (docs/plan §4).
/// </summary>
public class SatDbContext : DbContext
{
    public SatDbContext(DbContextOptions<SatDbContext> options) : base(options)
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

        // Nạp toàn bộ IEntityTypeConfiguration<> trong assembly này.
        // Thêm cấu hình mới chỉ cần tạo class, không phải sửa file này.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SatDbContext).Assembly);
    }
}
