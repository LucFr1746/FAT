using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SAT.Data;
using SAT.Domain.Enums;

namespace SAT.Tests.Integration;

/// <summary>
/// Kiểm tra mô hình EF có khớp với database thật do db/01_schema.sql tạo ra.
///
/// Vì sao cần: dự án không dùng Migrations nên KHÔNG có gì tự động bảo đảm
/// entity C# và bảng SQL đi cùng nhau. Gõ sai một tên cột thì build vẫn xanh,
/// và lỗi chỉ lộ ra lúc mở màn hình - thường là ngay trong buổi demo.
/// Mỗi truy vấn dưới đây ép EF sinh SQL thật cho một mapping, nên sai lệch
/// bị bắt ngay tại đây.
///
/// Cần chạy db/setup-db.ps1 trước. Nếu không kết nối được thì test tự SKIP
/// chứ không FAIL, để CI hoặc máy chưa cài SQL Server vẫn chạy được bộ test.
/// </summary>
public class DatabaseSchemaTests : IDisposable
{
    private const string ConnectionString =
        "Server=localhost;Database=SAT;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;Connect Timeout=5";

    private readonly SatDbContext? _db;
    private readonly bool _available;

    public DatabaseSchemaTests()
    {
        var options = new DbContextOptionsBuilder<SatDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        try
        {
            _db = new SatDbContext(options);
            _available = _db.Database.CanConnect();
        }
        catch
        {
            _available = false;
        }
    }

    public void Dispose() => _db?.Dispose();

    private SatDbContext RequireDb()
    {
        Skip.IfNot(_available, "Khong ket noi duoc SQL Server - hay chay db/setup-db.ps1 truoc.");
        return _db!;
    }

    [SkippableFact]
    public async Task Moi_DbSet_deu_truy_van_duoc_tren_schema_that()
    {
        var db = RequireDb();

        // Take(1) tren tung DbSet: ep EF sinh SELECT that cho MOI mapping.
        // Sai ten bang hoac ten cot se nem ngay tai day.
        (await db.Roles.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.Users.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.Majors.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.Students.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.Courses.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.Prerequisites.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.CurriculumItems.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.Semesters.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.Enrollments.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.Assessments.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.Grades.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.GradeScales.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.AcademicPlans.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.AcademicPlanItems.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.AuditLogs.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.Materials.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.MaterialFiles.Take(1).ToListAsync()).Should().NotBeNull();
    }

    [SkippableFact]
    public async Task Danh_sach_tai_lieu_khong_keo_theo_noi_dung_file()
    {
        var db = RequireDb();

        // Ly do tach Material / MaterialFile thanh 2 bang la de truy van danh
        // sach KHONG cham toi varbinary(max). Neu ai do gop lai mot bang hoac
        // them Include(File) vao truy van danh sach, test nay se do.
        var materials = await db.Materials
            .Include(m => m.Course)
            .OrderBy(m => m.MaterialId)
            .ToListAsync();

        materials.Should().HaveCount(8);
        materials.Should().OnlyContain(m => m.File == null,
            "truy van danh sach khong duoc nap noi dung nhi phan cua file");

        // Tai lieu dung chung khong gan mon hoc nao
        materials.Should().Contain(m => m.CourseId == null);
    }

    [SkippableFact]
    public async Task Tai_xuong_lay_dung_noi_dung_va_khop_kich_thuoc()
    {
        var db = RequireDb();

        var material = await db.Materials
            .Include(m => m.File)
            .FirstAsync(m => m.FileName == "PRN212-WPF-MVVM.txt");

        material.File.Should().NotBeNull();
        material.File!.Content.Should().NotBeEmpty();

        // FileSizeBytes trong metadata phai khop do dai that cua noi dung,
        // neu khong thi thanh tien trinh tai xuong se hien sai.
        material.File.Content.LongLength.Should().Be(material.FileSizeBytes);
    }

    [SkippableFact]
    public async Task Du_lieu_seed_co_dung_so_luong()
    {
        var db = RequireDb();

        (await db.Courses.CountAsync()).Should().Be(31);
        (await db.Students.CountAsync()).Should().Be(3);
        (await db.Semesters.CountAsync()).Should().Be(10);
        (await db.GradeScales.CountAsync()).Should().Be(8);
        (await db.Prerequisites.CountAsync()).Should().Be(19);
    }

    [SkippableFact]
    public async Task Enum_luu_thanh_chuoi_van_doc_nguoc_lai_dung()
    {
        var db = RequireDb();

        // Loc theo enum: EF phai dich EnrollmentStatus.Passed thanh chuoi
        // 'Passed' trong SQL. Cau hinh HasConversion sai thi ra 0 ket qua.
        var passed = await db.Enrollments
            .Where(e => e.Status == EnrollmentStatus.Passed)
            .CountAsync();

        passed.Should().BeGreaterThan(0, "seed co nhieu mon da dat");

        var studying = await db.Enrollments
            .Where(e => e.Status == EnrollmentStatus.Studying)
            .CountAsync();

        studying.Should().BeGreaterThan(0, "seed co mon cua ky hien tai dang hoc");
    }

    [SkippableFact]
    public async Task Navigation_property_join_duoc_qua_nhieu_bang()
    {
        var db = RequireDb();

        // Chuoi join 4 bang: Enrollment -> Student, Course, Semester.
        // Khai bao sai khoa ngoai o Configuration se lo ra ngay.
        var row = await db.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Course)
            .Include(e => e.Semester)
            .Where(e => e.Student!.StudentCode == "SE170001")
            .OrderBy(e => e.Semester!.DisplayOrder)
            .FirstOrDefaultAsync();

        row.Should().NotBeNull();
        row!.Course!.CourseCode.Should().NotBeNullOrWhiteSpace();
        row.Semester!.SemesterCode.Should().Be("SP24", "mon dau tien cua SE170001 nam o ky SP24");
    }

    [SkippableFact]
    public async Task Quan_he_hai_chieu_cua_Prerequisite_tro_dung_mon()
    {
        var db = RequireDb();

        // Prerequisite co HAI khoa ngoai cung tro ve Course. Neu cau hinh EF
        // gan nham chieu thi cau nay se tra ve mon sai ma van khong loi.
        var prn222 = await db.Prerequisites
            .Include(p => p.Course)
            .Include(p => p.RequiredCourse)
            .Where(p => p.Course!.CourseCode == "PRN222")
            .SingleAsync();

        prn222.RequiredCourse!.CourseCode.Should().Be("PRN212");
    }
}
