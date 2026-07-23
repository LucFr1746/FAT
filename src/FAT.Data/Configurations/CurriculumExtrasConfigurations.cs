using FAT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FAT.Data.Configurations;

/// <summary>Maps Term (a kỳ of the study path).</summary>
public class TermConfiguration : IEntityTypeConfiguration<Term>
{
    public void Configure(EntityTypeBuilder<Term> builder)
    {
        builder.ToTable("Term");
        builder.HasKey(x => x.TermId);
        builder.Property(x => x.TermName).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);

        // Unique because Curriculum.TermNo is a foreign key to THIS column
        // rather than to TermId - SQL Server requires the target of a foreign
        // key to be unique. Pointing at TermNo keeps the existing
        // Curriculum.TermNo values and every query over them working unchanged.
        builder.HasIndex(x => x.TermNo).IsUnique();
    }
}

/// <summary>Maps SubjectMaterial (syllabus readings and links).</summary>
public class SubjectMaterialConfiguration : IEntityTypeConfiguration<SubjectMaterial>
{
    public void Configure(EntityTypeBuilder<SubjectMaterial> builder)
    {
        builder.ToTable("SubjectMaterial");
        builder.HasKey(x => x.SubjectMaterialId);

        builder.Property(x => x.Title).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Url).HasMaxLength(500);
        builder.Property(x => x.Author).HasMaxLength(200);
        builder.Property(x => x.Publisher).HasMaxLength(200);
        builder.Property(x => x.Isbn).HasMaxLength(50);
        // No length cap: real FLM descriptions run past 700 characters.
        builder.Property(x => x.Description).HasColumnType("nvarchar(max)");

        // The import matches on this pair, so re-running an import updates the
        // row instead of adding a second copy of the same reading.
        builder.HasIndex(x => new { x.CourseId, x.Title }).IsUnique();

        builder.HasOne(x => x.Course)
               .WithMany(c => c.SubjectMaterials)
               .HasForeignKey(x => x.CourseId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Maps AssessmentSchedule (the syllabus timeline).</summary>
public class AssessmentScheduleConfiguration : IEntityTypeConfiguration<AssessmentSchedule>
{
    public void Configure(EntityTypeBuilder<AssessmentSchedule> builder)
    {
        builder.ToTable("AssessmentSchedule");
        builder.HasKey(x => x.AssessmentScheduleId);

        builder.Property(x => x.Title).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Description).HasColumnType("nvarchar(max)");
        builder.Property(x => x.TeachingType).HasMaxLength(100);

        // DATE, not datetime2: a time component would break "is this inside the
        // semester" comparisons the moment one row picked up 00:00:00.001.
        builder.Property(x => x.ExpectedDate).HasColumnType("date");

        builder.HasIndex(x => new { x.CourseId, x.SessionNo }).IsUnique();

        builder.HasOne(x => x.Course)
               .WithMany(c => c.AssessmentSchedules)
               .HasForeignKey(x => x.CourseId)
               .OnDelete(DeleteBehavior.Cascade);

        // Restrict, not Cascade: Course already cascades into both this table
        // and Assessment, and a second cascade path into the same table is
        // exactly what SQL Server refuses to create.
        builder.HasOne(x => x.Assessment)
               .WithMany()
               .HasForeignKey(x => x.AssessmentId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Maps GradePrediction (a saved GPA forecast).</summary>
public class GradePredictionConfiguration : IEntityTypeConfiguration<GradePrediction>
{
    public void Configure(EntityTypeBuilder<GradePrediction> builder)
    {
        builder.ToTable("GradePrediction");
        builder.HasKey(x => x.GradePredictionId);

        builder.Property(x => x.CurrentGpa).HasPrecision(4, 2);
        builder.Property(x => x.PredictedGpa).HasPrecision(4, 2);
        builder.Property(x => x.Note).HasMaxLength(500);

        // Enums as text, matching the rest of the schema, so a row read in SSMS
        // says 'VeryGood' rather than 4.
        builder.Property(x => x.BaseClassification)
               .HasConversion<string>()
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(x => x.AdjustedClassification)
               .HasConversion<string>()
               .HasMaxLength(20)
               .IsRequired();

        builder.HasIndex(x => new { x.StudentId, x.CreatedAt });

        builder.HasOne(x => x.Student)
               .WithMany()
               .HasForeignKey(x => x.StudentId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
