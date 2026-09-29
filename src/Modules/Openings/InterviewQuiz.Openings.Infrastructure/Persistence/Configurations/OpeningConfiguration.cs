using InterviewQuiz.Openings.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewQuiz.Openings.Infrastructure.Persistence.Configurations;

public sealed class OpeningConfiguration : IEntityTypeConfiguration<Opening>
{
    public void Configure(EntityTypeBuilder<Opening> builder)
    {
        builder.ToTable("openings");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Title).HasMaxLength(Opening.TitleMaxLength).IsRequired();
        builder.Property(e => e.JobDescription).HasMaxLength(Opening.JobDescriptionMaxLength).IsRequired();
        builder.Property(e => e.Owner).HasMaxLength(Opening.OwnerMaxLength).IsRequired();
        builder.Property(e => e.StartDate).IsRequired();
        builder.Property(e => e.Headcount).IsRequired();
        builder.Property(e => e.ExpectedExperienceYears).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();

        builder.Property(e => e.Handlers)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.Tags)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.RowVersion)
            .HasColumnName("row_version")
            .IsConcurrencyToken();

        builder.HasIndex(e => e.Owner);
        builder.HasIndex(e => e.StartDate);
        builder.HasIndex(e => e.ExpectedCloseDate);
        builder.HasIndex(e => e.ExpectedExperienceYears);
        builder.HasIndex(e => e.Tags)
            .HasMethod("gin");
    }
}
