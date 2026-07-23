using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SAT.Domain.Entities;

namespace SAT.Data.Configurations;

/// <summary>Ánh xạ AcademicPlan.</summary>
public class AcademicPlanConfiguration : IEntityTypeConfiguration<AcademicPlan>
{
    public void Configure(EntityTypeBuilder<AcademicPlan> builder)
    {
        builder.ToTable("AcademicPlan");
        builder.HasKey(x => x.PlanId);
        builder.Property(x => x.PlanName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);

        builder.HasOne(x => x.Student)
               .WithMany(s => s.AcademicPlans)
               .HasForeignKey(x => x.StudentId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Ánh xạ AcademicPlanItem.</summary>
public class AcademicPlanItemConfiguration : IEntityTypeConfiguration<AcademicPlanItem>
{
    public void Configure(EntityTypeBuilder<AcademicPlanItem> builder)
    {
        builder.ToTable("AcademicPlanItem");
        builder.HasKey(x => x.PlanItemId);
        builder.Property(x => x.ExpectedScore).HasPrecision(4, 2);

        builder.HasIndex(x => new { x.PlanId, x.CourseId }).IsUnique();

        builder.HasOne(x => x.Plan)
               .WithMany(p => p.Items)
               .HasForeignKey(x => x.PlanId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Course)
               .WithMany()
               .HasForeignKey(x => x.CourseId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Semester)
               .WithMany()
               .HasForeignKey(x => x.SemesterId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Ánh xạ AuditLog.</summary>
public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLog");
        builder.HasKey(x => x.AuditLogId);
        builder.Property(x => x.Action).HasMaxLength(50).IsRequired();
        builder.Property(x => x.EntityName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.EntityId).HasMaxLength(50);

        // Restrict: xóa tài khoản không được xóa mất dấu vết thao tác của nó.
        builder.HasOne(x => x.User)
               .WithMany()
               .HasForeignKey(x => x.UserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
