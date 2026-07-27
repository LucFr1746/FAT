using Data;
using Domain.Entities;
using FluentAssertions;
using Services.Dtos;
using Services.Implementations;
using Tests.TestSupport;

namespace Tests.Services;

/// <summary>
/// The material library - Member 5's browse/search over the FLM syllabus links.
/// </summary>
public class MaterialLibraryServiceTests
{
    // Admin context by default: unscoped, so the filter/search tests see every
    // material regardless of major. The major-scoping test builds its own context.
    private static MaterialLibraryService CreateService(FAT_DBContext db)
        => new(db, TestCurrentUserContext.Admin());

    private static SubjectMaterial AddMaterial(
        FAT_DBContext db,
        int courseId,
        string title,
        string? url = null,
        string? author = null,
        string? publisher = null,
        int displayOrder = 0,
        bool isActive = true)
    {
        var material = new SubjectMaterial
        {
            CourseId = courseId,
            Title = title,
            Url = url,
            Author = author,
            Publisher = publisher,
            DisplayOrder = displayOrder,
            IsActive = isActive
        };

        db.SubjectMaterials.Add(material);
        db.SaveChanges();
        return material;
    }

    [Fact]
    public async Task SearchAsync_returns_active_materials_with_course_details()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");
        AddMaterial(db, course.CourseId, "Slide chương 1", url: "https://flm.fpt.edu.vn/1.zip");

        var results = await CreateService(db).SearchAsync(new MaterialLibraryFilter());

        results.Should().ContainSingle();
        var item = results[0];
        item.Title.Should().Be("Slide chương 1");
        item.CourseCode.Should().Be("PRF192");
        item.SubjectDisplay.Should().Be("PRF192 - Course PRF192");
        item.HasLink.Should().BeTrue();
    }

    [Fact]
    public async Task SearchAsync_excludes_inactive_materials()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");
        AddMaterial(db, course.CourseId, "Còn hiệu lực");
        AddMaterial(db, course.CourseId, "Đã ẩn", isActive: false);

        var results = await CreateService(db).SearchAsync(new MaterialLibraryFilter());

        results.Should().ContainSingle().Which.Title.Should().Be("Còn hiệu lực");
    }

    [Fact]
    public async Task SearchAsync_filters_by_course()
    {
        using var db = TestDb.CreateWithReferenceData();
        var prf = db.AddCourse("PRF192");
        var mad = db.AddCourse("MAD101");
        AddMaterial(db, prf.CourseId, "Tài liệu PRF");
        AddMaterial(db, mad.CourseId, "Tài liệu MAD");

        var results = await CreateService(db).SearchAsync(new MaterialLibraryFilter(CourseId: mad.CourseId));

        results.Should().ContainSingle().Which.Title.Should().Be("Tài liệu MAD");
    }

    [Fact]
    public async Task SearchAsync_only_downloadable_hides_materials_without_a_link()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");
        AddMaterial(db, course.CourseId, "Có link", url: "https://flm.fpt.edu.vn/a.zip");
        AddMaterial(db, course.CourseId, "Sách in", url: null);

        var results = await CreateService(db).SearchAsync(new MaterialLibraryFilter(OnlyDownloadable: true));

        results.Should().ContainSingle().Which.Title.Should().Be("Có link");
    }

    [Theory]
    [InlineData("Knuth")]      // author
    [InlineData("Pearson")]    // publisher
    [InlineData("PRF192")]     // course code
    [InlineData("algorithm")]  // title
    public async Task SearchAsync_matches_keyword_across_fields(string keyword)
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");
        AddMaterial(db, course.CourseId, "The Art of algorithm", author: "Knuth", publisher: "Pearson");
        AddMaterial(db, db.AddCourse("MAD101").CourseId, "Unrelated reading", author: "Someone");

        var results = await CreateService(db).SearchAsync(new MaterialLibraryFilter(Keyword: keyword));

        results.Should().ContainSingle().Which.Title.Should().Be("The Art of algorithm");
    }

    [Fact]
    public async Task SearchAsync_orders_by_course_code_then_display_order()
    {
        using var db = TestDb.CreateWithReferenceData();
        var mad = db.AddCourse("MAD101");
        var prf = db.AddCourse("PRF192");
        AddMaterial(db, prf.CourseId, "PRF second", displayOrder: 1);
        AddMaterial(db, prf.CourseId, "PRF first", displayOrder: 0);
        AddMaterial(db, mad.CourseId, "MAD only", displayOrder: 0);

        var results = await CreateService(db).SearchAsync(new MaterialLibraryFilter());

        results.Select(r => r.Title).Should().ContainInOrder("MAD only", "PRF first", "PRF second");
    }

    private static Material AddUploadedMaterial(FAT_DBContext db, int courseId, string title)
    {
        var material = new Material
        {
            CourseId = courseId,
            Title = title,
            Category = "Slide",
            FileName = "file.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 3,
            ContentHash = System.Guid.NewGuid().ToString("N"),
            UploadedAt = System.DateTime.Now,
            IsActive = true,
            File = new MaterialFile { Content = [1, 2, 3] }
        };
        db.Materials.Add(material);
        db.SaveChanges();
        return material;
    }

    [Fact]
    public async Task SearchAsync_includes_uploaded_files_alongside_links()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("SWT301");
        AddMaterial(db, course.CourseId, "FLM link", url: "https://flm.fpt.edu.vn/x.zip");
        AddUploadedMaterial(db, course.CourseId, "Uploaded slides");

        var results = await CreateService(db).SearchAsync(new MaterialLibraryFilter());

        results.Should().HaveCount(2);
        results.Should().ContainSingle(r => r.IsUploadedFile && r.Title == "Uploaded slides")
            .Which.CanDownload.Should().BeTrue();
        results.Should().ContainSingle(r => !r.IsUploadedFile && r.HasLink && r.Title == "FLM link");
    }

    [Fact]
    public async Task SearchAsync_filters_by_major_for_an_admin()
    {
        using var db = TestDb.CreateWithReferenceData();
        var (se, ai, seCourse, aiCourse) = TwoMajorsWithMaterials(db);

        var results = await CreateService(db).SearchAsync(new MaterialLibraryFilter(MajorId: se.MajorId));

        results.Should().ContainSingle().Which.Title.Should().Be("SE reading");
        _ = (ai, aiCourse, seCourse);
    }

    [Fact]
    public async Task GetSubjectOptionsAsync_narrows_to_the_chosen_major()
    {
        using var db = TestDb.CreateWithReferenceData();
        var (se, _, seCourse, _) = TwoMajorsWithMaterials(db);

        var options = await CreateService(db).GetSubjectOptionsAsync(majorId: se.MajorId);

        options.Should().ContainSingle().Which.CourseId.Should().Be(seCourse.CourseId);
    }

    [Fact]
    public async Task SearchAsync_filters_by_term()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE", "Software Engineering");
        var term1Course = db.AddCourse("PRF192");
        var term2Course = db.AddCourse("MAD101");
        db.AddCurriculumItem(major.MajorId, term1Course.CourseId, termNo: 1);
        db.AddCurriculumItem(major.MajorId, term2Course.CourseId, termNo: 2);
        AddMaterial(db, term1Course.CourseId, "Term 1 material");
        AddMaterial(db, term2Course.CourseId, "Term 2 material");

        var results = await CreateService(db).SearchAsync(new MaterialLibraryFilter(TermNo: 1));

        results.Should().ContainSingle().Which.Title.Should().Be("Term 1 material");
    }

    [Fact]
    public async Task GetMajorOptionsAsync_lists_the_programmes()
    {
        using var db = TestDb.CreateWithReferenceData();
        db.AddMajor("SE", "Software Engineering");
        db.AddMajor("AI", "Artificial Intelligence");

        var majors = await CreateService(db).GetMajorOptionsAsync();

        majors.Select(m => m.MajorCode).Should().BeEquivalentTo(["SE", "AI"]);
    }

    private static (Domain.Entities.Major Se, Domain.Entities.Major Ai,
        Domain.Entities.Course SeCourse, Domain.Entities.Course AiCourse) TwoMajorsWithMaterials(FAT_DBContext db)
    {
        var se = db.AddMajor("SE", "Software Engineering");
        var ai = db.AddMajor("AI", "Artificial Intelligence");
        var seCourse = db.AddCourse("PRF192");
        var aiCourse = db.AddCourse("AIL301");
        db.AddCurriculumItem(se.MajorId, seCourse.CourseId, termNo: 1);
        db.AddCurriculumItem(ai.MajorId, aiCourse.CourseId, termNo: 1);
        AddMaterial(db, seCourse.CourseId, "SE reading");
        AddMaterial(db, aiCourse.CourseId, "AI reading");
        return (se, ai, seCourse, aiCourse);
    }

    [Fact]
    public async Task SearchAsync_scopes_to_the_signed_in_student_major()
    {
        using var db = TestDb.CreateWithReferenceData();
        var seMajor = db.AddMajor("SE", "Software Engineering");
        var aiMajor = db.AddMajor("AI", "Artificial Intelligence");

        var seCourse = db.AddCourse("PRF192");
        var aiCourse = db.AddCourse("AIL301");
        db.AddCurriculumItem(seMajor.MajorId, seCourse.CourseId, termNo: 1);
        db.AddCurriculumItem(aiMajor.MajorId, aiCourse.CourseId, termNo: 1);

        AddMaterial(db, seCourse.CourseId, "SE reading");
        AddMaterial(db, aiCourse.CourseId, "AI reading");

        var seStudent = db.AddStudent(seMajor.MajorId, "SE000001");
        var service = new MaterialLibraryService(db, TestCurrentUserContext.Student(seStudent.StudentId));

        var results = await service.SearchAsync(new MaterialLibraryFilter());

        results.Should().ContainSingle().Which.Title.Should().Be("SE reading");
    }

    [Fact]
    public async Task GetSubjectOptionsAsync_returns_distinct_subjects_that_have_materials()
    {
        using var db = TestDb.CreateWithReferenceData();
        var prf = db.AddCourse("PRF192");
        db.AddCourse("CSD201"); // no materials -> must not appear
        AddMaterial(db, prf.CourseId, "One");
        AddMaterial(db, prf.CourseId, "Two");

        var options = await CreateService(db).GetSubjectOptionsAsync();

        options.Should().ContainSingle().Which.CourseCode.Should().Be("PRF192");
    }
}
