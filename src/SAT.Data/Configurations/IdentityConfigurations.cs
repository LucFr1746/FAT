using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SAT.Domain.Entities;
using SAT.Domain.Enums;

namespace SAT.Data.Configurations;

/// <summary>Ánh xạ Role - phải khớp dbo.Role trong db/01_schema.sql.</summary>
public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Role");
        builder.HasKey(x => x.RoleId);
        builder.Property(x => x.RoleName).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(200);
        builder.HasIndex(x => x.RoleName).IsUnique();
    }
}

/// <summary>Ánh xạ AppUser.</summary>
public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("AppUser");
        builder.HasKey(x => x.UserId);
        builder.Property(x => x.Username).HasMaxLength(50).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
        builder.HasIndex(x => x.Username).IsUnique();

        builder.HasOne(x => x.Role)
               .WithMany(r => r.Users)
               .HasForeignKey(x => x.RoleId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Ánh xạ Major.</summary>
public class MajorConfiguration : IEntityTypeConfiguration<Major>
{
    public void Configure(EntityTypeBuilder<Major> builder)
    {
        builder.ToTable("Major");
        builder.HasKey(x => x.MajorId);
        builder.Property(x => x.MajorCode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.MajorName).HasMaxLength(150).IsRequired();
        builder.HasIndex(x => x.MajorCode).IsUnique();
    }
}

/// <summary>Ánh xạ Student.</summary>
public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Student");
        builder.HasKey(x => x.StudentId);
        builder.Property(x => x.StudentCode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(150);

        // Cột DB là DATE (không có phần giờ). Không khai báo rõ thì EF gửi
        // datetime2 và so sánh ngày sẽ lệch khi có phần giờ khác 0.
        builder.Property(x => x.DateOfBirth).HasColumnType("date");
        builder.Property(x => x.EnrollmentDate).HasColumnType("date");

        // Lưu enum thành CHUỖI: mở SSMS đọc thấy 'Active' thay vì số 0.
        builder.Property(x => x.Status)
               .HasConversion<string>()
               .HasMaxLength(20)
               .IsRequired();

        builder.HasIndex(x => x.StudentCode).IsUnique();
        builder.HasIndex(x => x.UserId).IsUnique();

        builder.HasOne(x => x.User)
               .WithOne(u => u.Student)
               .HasForeignKey<Student>(x => x.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Major)
               .WithMany(m => m.Students)
               .HasForeignKey(x => x.MajorId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
