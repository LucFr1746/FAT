using FAT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FAT.Data.Configurations;

/// <summary>Maps Course.</summary>
public class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("Course");
        builder.HasKey(x => x.CourseId);
        builder.Property(x => x.CourseCode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.CourseName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
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

        builder.HasOne(x => x.Major)
               .WithMany(m => m.CurriculumItems)
               .HasForeignKey(x => x.MajorId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Course)
               .WithMany(c => c.CurriculumItems)
               .HasForeignKey(x => x.CourseId)
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
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();

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
