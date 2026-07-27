using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Data.Configurations;

/// <summary>Maps Material (learning material metadata).</summary>
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

        // Fixed CHAR(64): a hex-encoded SHA-256 is always exactly 64 characters.
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

/// <summary>Maps MaterialFile (the binary payload).</summary>
public class MaterialFileConfiguration : IEntityTypeConfiguration<MaterialFile>
{
    public void Configure(EntityTypeBuilder<MaterialFile> builder)
    {
        builder.ToTable("MaterialFile");

        // The primary key IS the foreign key - a shared-key one-to-one.
        builder.HasKey(x => x.MaterialId);
        builder.Property(x => x.MaterialId).ValueGeneratedNever();

        builder.Property(x => x.Content).HasColumnType("varbinary(max)").IsRequired();
    }
}
