using FAT.Data;
using FAT.Domain.Entities;
using FAT.Domain.Enums;
using FAT.Services.Implementations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FAT.Tests.Unit;

public class GradeGpaServiceTests
{
    [Fact]
    public async Task Gpa_counts_only_latest_passed_attempt_and_weights_credits()
    {
        await using var db = CreateDb(); SeedCatalog(db);
        db.Enrollments.AddRange(
            new Enrollment { EnrollmentId = 1, StudentId = 1, CourseId = 1, SemesterId = 1, Status = EnrollmentStatus.Passed, FinalScore = 6, IsCounted = false },
            new Enrollment { EnrollmentId = 2, StudentId = 1, CourseId = 1, SemesterId = 2, Status = EnrollmentStatus.Passed, FinalScore = 8, IsCounted = true },
            new Enrollment { EnrollmentId = 3, StudentId = 1, CourseId = 2, SemesterId = 2, Status = EnrollmentStatus.Passed, FinalScore = 10, IsCounted = true });
        await db.SaveChangesAsync();
        (await new GpaService(db).GetCumulativeGpaAsync(1)).Should().Be(8.8m);
    }

    [Fact]
    public async Task Grade_upsert_settles_complete_course_and_honours_component_minimum()
    {
        await using var db = CreateDb(); SeedCatalog(db);
        db.Assessments.AddRange(new Assessment { AssessmentId = 1, CourseId = 1, Name = "Assignment", Weight = .5m },
            new Assessment { AssessmentId = 2, CourseId = 1, Name = "Final", Weight = .5m, MinScoreToPass = 4 });
        db.GradeScales.AddRange(new GradeScale { GradeScaleId = 1, MinScore = 0, MaxScore = 5, LetterGrade = "F", GradePoint = 0 },
            new GradeScale { GradeScaleId = 2, MinScore = 5, MaxScore = 10.01m, LetterGrade = "B", GradePoint = 3 });
        db.Enrollments.Add(new Enrollment { EnrollmentId = 1, StudentId = 1, CourseId = 1, SemesterId = 1 }); await db.SaveChangesAsync();
        var service = new GradeService(db);
        await service.UpsertGradeAsync(1, 1, 8); await service.UpsertGradeAsync(1, 2, 3);
        var enrollment = await db.Enrollments.SingleAsync();
        enrollment.FinalScore.Should().Be(5.5m); enrollment.Status.Should().Be(EnrollmentStatus.Failed);
    }

    private static FatDbContext CreateDb() => new(new DbContextOptionsBuilder<FatDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static void SeedCatalog(FatDbContext db)
    {
        db.Majors.Add(new Major { MajorId = 1, RequiredCredits = 10 });
        db.Students.Add(new Student { StudentId = 1, MajorId = 1 });
        db.Courses.AddRange(new Course { CourseId = 1, CourseCode = "A", Credits = 3 }, new Course { CourseId = 2, CourseCode = "B", Credits = 2 });
        db.Semesters.AddRange(new Semester { SemesterId = 1, SemesterCode = "S1", DisplayOrder = 1 }, new Semester { SemesterId = 2, SemesterCode = "S2", DisplayOrder = 2 });
        db.SaveChanges();
    }
}
