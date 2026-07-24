using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Data.Configurations;

/// <summary>Maps Course.</summary>
public class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("Course");
        builder.HasKey(x => x.CourseId);
        builder.Property(x => x.CourseCode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.CourseName).HasMaxLength(200).IsRequired();

        // nvarchar(max), NOT 500: the longest real FLM syllabus description is
        // over 6,000 characters, so a capped column silently truncates it.
        builder.Property(x => x.Description).HasColumnType("nvarchar(max)");

        builder.Property(x => x.MinAvgMarkToPass).HasPrecision(4, 2);
        builder.Property(x => x.SyllabusCode).HasMaxLength(20);
        builder.Property(x => x.PrerequisiteText).HasMaxLength(500);

        builder.HasIndex(x => x.CourseCode).IsUnique();
    }
}

/// <summary>
/// Maps Prerequisite.
///
/// Both foreign keys point back at Course, so Restrict is MANDATORY: SQL Server
/// refuses to create a table with two cascade paths into the same table.
/// The two relationships must also be declared explicitly, because EF cannot
/// tell which one is "the course with the requirement" and which one is
/// "the course that must come first".
/// </summary>
public class PrerequisiteConfiguration : IEntityTypeConfiguration<Prerequisite>
{
    public void Configure(EntityTypeBuilder<Prerequisite> builder)
    {
        builder.ToTable("Prerequisite");
        builder.HasKey(x => x.PrerequisiteId);

        builder.Property(x => x.Type)
               .HasConversion<string>()
               .HasMaxLength(20)
               .IsRequired();

        builder.HasIndex(x => new { x.CourseId, x.RequiredCourseId }).IsUnique();

        // Rows sharing a GroupNo above zero are alternatives (OR); the index
        // supports the grouping the eligibility check does on every lookup.
        builder.HasIndex(x => new { x.CourseId, x.GroupNo });

        builder.HasOne(x => x.Course)
               .WithMany(c => c.Prerequisites)
               .HasForeignKey(x => x.CourseId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RequiredCourse)
               .WithMany(c => c.RequiredFor)
               .HasForeignKey(x => x.RequiredCourseId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Maps Curriculum (the degree study path).</summary>
public class CurriculumConfiguration : IEntityTypeConfiguration<Curriculum>
{
    public void Configure(EntityTypeBuilder<Curriculum> builder)
    {
        builder.ToTable("Curriculum");
        builder.HasKey(x => x.CurriculumId);
        builder.HasIndex(x => new { x.MajorId, x.CourseId }).IsUnique();

        // Covers the curriculum screen's natural ordering: a major's subjects,
        // by term, in the order the reorder feature stores.
        builder.HasIndex(x => new { x.MajorId, x.TermNo, x.DisplayOrder });

        builder.HasOne(x => x.Major)
               .WithMany(m => m.CurriculumItems)
               .HasForeignKey(x => x.MajorId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Course)
               .WithMany(c => c.CurriculumItems)
               .HasForeignKey(x => x.CourseId)
               .OnDelete(DeleteBehavior.Restrict);

        // Foreign key onto Term.TermNo (a unique column), not Term.TermId, so
        // the existing TermNo values and every query over them stay valid.
        // Restrict: deleting a kỳ must not silently delete a curriculum.
        builder.HasOne(x => x.Term)
               .WithMany(t => t.CurriculumItems)
               .HasForeignKey(x => x.TermNo)
               .HasPrincipalKey(t => t.TermNo)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Maps Semester.</summary>
public class SemesterConfiguration : IEntityTypeConfiguration<Semester>
{
    public void Configure(EntityTypeBuilder<Semester> builder)
    {
        builder.ToTable("Semester");
        builder.HasKey(x => x.SemesterId);
        builder.Property(x => x.SemesterCode).HasMaxLength(10).IsRequired();
        builder.Property(x => x.SemesterName).HasMaxLength(50).IsRequired();
        builder.Property(x => x.StartDate).HasColumnType("date");
        builder.Property(x => x.EndDate).HasColumnType("date");

        builder.HasIndex(x => x.SemesterCode).IsUnique();
        builder.HasIndex(x => x.DisplayOrder).IsUnique();
    }
}

/// <summary>Maps Assessment (a grade component of a course).</summary>
public class AssessmentConfiguration : IEntityTypeConfiguration<Assessment>
{
    public void Configure(EntityTypeBuilder<Assessment> builder)
    {
        builder.ToTable("Assessment");
        builder.HasKey(x => x.AssessmentId);
        // 200, not 100: FLM category names reach 156 characters
        // ("Tham gia trên lớpParticipation" and friends).
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();

        // Four decimal places so that awkward ratios such as one third can be
        // represented while the components still sum to exactly 1.00.
        builder.Property(x => x.Weight).HasPrecision(5, 4);
        builder.Property(x => x.MinScoreToPass).HasPrecision(4, 2);

        builder.HasIndex(x => new { x.CourseId, x.Name }).IsUnique();

        builder.HasOne(x => x.Course)
               .WithMany(c => c.Assessments)
               .HasForeignKey(x => x.CourseId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Maps GradeScale (the score conversion table).</summary>
public class GradeScaleConfiguration : IEntityTypeConfiguration<GradeScale>
{
    public void Configure(EntityTypeBuilder<GradeScale> builder)
    {
        builder.ToTable("GradeScale");
        builder.HasKey(x => x.GradeScaleId);
        builder.Property(x => x.MinScore).HasPrecision(4, 2);
        builder.Property(x => x.MaxScore).HasPrecision(4, 2);
        builder.Property(x => x.LetterGrade).HasMaxLength(5).IsRequired();
        builder.Property(x => x.GradePoint).HasPrecision(3, 2);
        builder.Property(x => x.Description).HasMaxLength(50);

        builder.HasIndex(x => x.LetterGrade).IsUnique();
    }
}
