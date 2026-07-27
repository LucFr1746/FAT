using FAT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FAT.Data.Configurations;

/// <summary>Maps Enrollment - the central table of the application.</summary>
public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("Enrollment");
        builder.HasKey(x => x.EnrollmentId);

        builder.Property(x => x.Status)
               .HasConversion<string>()
               .HasMaxLength(20)
               .IsRequired();

        // decimal, not double. Binary rounding error accumulated across dozens
        // of courses is enough to move the GPA in its second decimal place.
        builder.Property(x => x.FinalScore).HasPrecision(4, 2);
        builder.Property(x => x.GradePoint).HasPrecision(3, 2);
        builder.Property(x => x.LetterGrade).HasMaxLength(5);

        // Block duplicate registrations at the database level. Validating in C#
        // is still worth doing, but this constraint is the backstop that no
        // code path can slip past.
        builder.HasIndex(x => new { x.StudentId, x.CourseId, x.SemesterId }).IsUnique();

        builder.HasOne(x => x.Student)
               .WithMany(s => s.Enrollments)
               .HasForeignKey(x => x.StudentId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Course)
               .WithMany(c => c.Enrollments)
               .HasForeignKey(x => x.CourseId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Semester)
               .WithMany(s => s.Enrollments)
               .HasForeignKey(x => x.SemesterId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Maps Grade (an individual component score).</summary>
public class GradeConfiguration : IEntityTypeConfiguration<Grade>
{
    public void Configure(EntityTypeBuilder<Grade> builder)
    {
        builder.ToTable("Grade");
        builder.HasKey(x => x.GradeId);
        builder.Property(x => x.Score).HasPrecision(4, 2);

        builder.HasIndex(x => new { x.EnrollmentId, x.AssessmentId }).IsUnique();

        builder.HasOne(x => x.Enrollment)
               .WithMany(e => e.Grades)
               .HasForeignKey(x => x.EnrollmentId)
               .OnDelete(DeleteBehavior.Cascade);

        // Restrict is required here: cascading would give deletion of a Course
        // two paths into Grade (via Assessment and via Enrollment), which SQL
        // Server rejects outright.
        builder.HasOne(x => x.Assessment)
               .WithMany(a => a.Grades)
               .HasForeignKey(x => x.AssessmentId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
