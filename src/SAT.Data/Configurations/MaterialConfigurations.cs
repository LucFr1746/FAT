using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SAT.Domain.Entities;

namespace SAT.Data.Configurations;

/// <summary>Ánh xạ Material (metadata tài liệu).</summary>
public class MaterialConfiguration : IEntityTypeConfiguration<Material>
{
    public void Configure(EntityTypeBuilder<Material> builder)
    {
        builder.ToTable("Material");
        builder.HasKey(x => x.MaterialId);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Category).HasMaxLength(30).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();

        // CHAR(64) cố định: SHA-256 ở dạng hex luôn đúng 64 ký tự.
        builder.Property(x => x.ContentHash).HasColumnType("char(64)");

        builder.HasOne(x => x.Course)
               .WithMany()
               .HasForeignKey(x => x.CourseId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UploadedBy)
               .WithMany()
               .HasForeignKey(x => x.UploadedByUserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.File)
               .WithOne(f => f.Material)
               .HasForeignKey<MaterialFile>(f => f.MaterialId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Ánh xạ MaterialFile (nội dung nhị phân).</summary>
public class MaterialFileConfiguration : IEntityTypeConfiguration<MaterialFile>
{
    public void Configure(EntityTypeBuilder<MaterialFile> builder)
    {
        builder.ToTable("MaterialFile");

        // Khóa chính CHÍNH LÀ khóa ngoại - quan hệ 1-1 dùng chung khóa.
        builder.HasKey(x => x.MaterialId);
        builder.Property(x => x.MaterialId).ValueGeneratedNever();

        builder.Property(x => x.Content).HasColumnType("varbinary(max)").IsRequired();
    }
}
