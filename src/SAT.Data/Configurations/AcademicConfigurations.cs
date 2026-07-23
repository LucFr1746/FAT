using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SAT.Domain.Entities;

namespace SAT.Data.Configurations;

/// <summary>Ánh xạ Enrollment - bảng trung tâm của ứng dụng.</summary>
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

        // decimal chứ không phải double. Cộng dồn qua hàng chục môn thì sai số
        // nhị phân của double đủ để làm GPA lệch ở chữ số thập phân thứ hai.
        builder.Property(x => x.FinalScore).HasPrecision(4, 2);
        builder.Property(x => x.GradePoint).HasPrecision(3, 2);
        builder.Property(x => x.LetterGrade).HasMaxLength(5);

        // Chặn đăng ký trùng ngay ở tầng DB. Validate trong C# vẫn cần, nhưng
        // ràng buộc ở đây là lớp chặn cuối không ai lách qua được.
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

/// <summary>Ánh xạ Grade (điểm thành phần).</summary>
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

        // Restrict bắt buộc: nếu cascade thì xóa Course sẽ có hai đường tới
        // Grade (qua Assessment và qua Enrollment) - SQL Server không cho phép.
        builder.HasOne(x => x.Assessment)
               .WithMany(a => a.Grades)
               .HasForeignKey(x => x.AssessmentId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
