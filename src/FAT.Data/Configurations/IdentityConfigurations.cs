using FAT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FAT.Data.Configurations;

/// <summary>Maps Role - must match dbo.Role in db/01_schema.sql.</summary>
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

/// <summary>Maps AppUser.</summary>
public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("AppUser");
        builder.HasKey(x => x.UserId);
        builder.Property(x => x.Username).HasMaxLength(50).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired(false);
        builder.Property(x => x.GoogleId).HasMaxLength(255);
        builder.Property(x => x.AvatarUrl).HasMaxLength(1000);
        builder.HasIndex(x => x.Username).IsUnique();
        builder.HasIndex(x => x.GoogleId).IsUnique();

        builder.HasOne(x => x.Role)
               .WithMany(r => r.Users)
               .HasForeignKey(x => x.RoleId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Maps Major.</summary>
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

/// <summary>Maps Student.</summary>
public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Student");
        builder.HasKey(x => x.StudentId);
        builder.Property(x => x.StudentCode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(150);
        builder.Property(x => x.CurrentSemester).HasMaxLength(20);
        builder.Ignore(x => x.Campus);

        // The columns are DATE (no time part). Without saying so explicitly EF
        // sends datetime2, and date comparisons drift once a time component
        // sneaks in.
        builder.Property(x => x.DateOfBirth).HasColumnType("date");
        builder.Property(x => x.EnrollmentDate).HasColumnType("date");

        // Store the enum as TEXT so SSMS shows 'Active' instead of 0.
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
