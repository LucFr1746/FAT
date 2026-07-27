using Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Services.Abstractions;
using Services.Dtos;
using Services.Implementations;
using Tests.TestSupport;

namespace Tests.Services;

/// <summary>Uploaded file materials - Member 5's Upload / Download side.</summary>
public class MaterialServiceTests
{
    private static MaterialService CreateService(FAT_DBContext db, bool asAdmin = true)
        => new(db, asAdmin ? TestCurrentUserContext.Admin() : TestCurrentUserContext.Student(1));

    private static MaterialUploadRequest Request(
        int courseId, string title = "Slides", string fileName = "slides.pdf",
        string category = MaterialCategories.Slide, byte[]? content = null)
        => new(courseId, title, null, category, fileName, "application/pdf", content ?? [1, 2, 3, 4]);

    [Fact]
    public async Task UploadAsync_stores_metadata_and_file_bytes()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("SWT301");

        var id = await CreateService(db).UploadAsync(Request(course.CourseId), uploadedByUserId: 1);

        var stored = await db.Materials.Include(m => m.File).SingleAsync(m => m.MaterialId == id);
        stored.Title.Should().Be("Slides");
        stored.FileSizeBytes.Should().Be(4);
        stored.ContentHash.Should().NotBeNullOrEmpty();
        stored.File!.Content.Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public async Task UploadAsync_requires_an_admin()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("SWT301");

        var act = () => CreateService(db, asAdmin: false).UploadAsync(Request(course.CourseId), 1);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task UploadAsync_rejects_an_empty_file()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("SWT301");

        var act = () => CreateService(db).UploadAsync(Request(course.CourseId, content: []), 1);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*rỗng*");
    }

    [Fact]
    public async Task UploadAsync_rejects_a_file_over_the_size_cap()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("SWT301");
        var tooBig = new byte[IMaterialService.MaxFileSizeBytes + 1];

        var act = () => CreateService(db).UploadAsync(Request(course.CourseId, content: tooBig), 1);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*MB*");
    }

    [Fact]
    public async Task UploadAsync_rejects_an_unknown_category()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("SWT301");

        var act = () => CreateService(db).UploadAsync(Request(course.CourseId, category: "Nonsense"), 1);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Danh mục*");
    }

    [Fact]
    public async Task UploadAsync_strips_path_parts_from_the_file_name()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("SWT301");

        var id = await CreateService(db).UploadAsync(
            Request(course.CourseId, fileName: @"..\..\Windows\evil.exe"), 1);

        var stored = await db.Materials.SingleAsync(m => m.MaterialId == id);
        stored.FileName.Should().Be("evil.exe");
    }

    [Fact]
    public async Task UploadAsync_rejects_duplicate_content_in_the_same_course()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("SWT301");
        var content = new byte[] { 9, 8, 7 };
        var service = CreateService(db);

        await service.UploadAsync(Request(course.CourseId, content: content), 1);
        var act = () => service.UploadAsync(Request(course.CourseId, title: "Copy", content: content), 1);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*trùng*");
    }

    [Fact]
    public async Task UploadAsync_allows_the_same_content_in_a_different_course()
    {
        using var db = TestDb.CreateWithReferenceData();
        var a = db.AddCourse("SWT301");
        var b = db.AddCourse("PRF192");
        var content = new byte[] { 5, 5, 5 };
        var service = CreateService(db);

        await service.UploadAsync(Request(a.CourseId, content: content), 1);
        var act = () => service.UploadAsync(Request(b.CourseId, content: content), 1);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DownloadAsync_returns_bytes_and_increments_the_counter()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("SWT301");
        var service = CreateService(db);
        var id = await service.UploadAsync(Request(course.CourseId, content: [4, 2]), 1);

        var download = await service.DownloadAsync(id);
        await service.DownloadAsync(id);

        download.Should().NotBeNull();
        download!.Content.Should().Equal(4, 2);
        (await db.Materials.AsNoTracking().SingleAsync(m => m.MaterialId == id))
            .DownloadCount.Should().Be(2);
    }

    [Fact]
    public async Task DownloadAsync_returns_null_for_a_missing_material()
    {
        using var db = TestDb.CreateWithReferenceData();

        (await CreateService(db).DownloadAsync(9999)).Should().BeNull();
    }

    [Fact]
    public async Task DeactivateAsync_hides_the_material_without_deleting_it()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("SWT301");
        var service = CreateService(db);
        var id = await service.UploadAsync(Request(course.CourseId), 1);

        await service.DeactivateAsync(id);

        var stored = await db.Materials.SingleAsync(m => m.MaterialId == id);
        stored.IsActive.Should().BeFalse();
    }
}
