using InterviewQuiz.Evaluation.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewQuiz.Evaluation.Infrastructure.Persistence.Configurations;

public sealed class AttemptConfiguration : IEntityTypeConfiguration<Attempt>
{
    public void Configure(EntityTypeBuilder<Attempt> builder)
    {
        builder.ToTable("attempts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.AssignmentId).IsRequired();
        builder.Property(e => e.OpeningId).IsRequired();
        builder.Property(e => e.SnapshotId).IsRequired();
        builder.Property(e => e.CandidateEmail).HasMaxLength(256).IsRequired();
        builder.Property(e => e.StartedAtUtc).IsRequired();
        builder.Property(e => e.DueAtUtc).IsRequired();
        builder.Property(e => e.SubmittedAtUtc);
        builder.Property(e => e.AutoPointsAwarded).HasPrecision(18, 4);
        builder.Property(e => e.AutoPointsAvailable).HasPrecision(18, 4);
        builder.Property(e => e.TotalPointsAvailable).HasPrecision(18, 4);

        builder.Property(e => e.Status)
            .HasConversion(CamelCaseEnumConverter.For<AttemptStatus>())
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.ResultStatus)
            .HasConversion(CamelCaseEnumConverter.ForNullable<ResultStatus>())
            .HasMaxLength(32);

        builder.Property(e => e.PromptOrderJson)
            .HasColumnType("jsonb")
            .HasConversion(JsonDocumentConverter.Converter)
            .Metadata.SetValueComparer(JsonDocumentConverter.Comparer);

        builder.Property(e => e.RowVersion)
            .HasColumnName("row_version")
            .IsConcurrencyToken();

        builder.HasIndex(e => e.AssignmentId);
        builder.HasIndex(e => new { e.AssignmentId, e.Status });
        builder.HasIndex(e => e.AssignmentId)
            .HasFilter("status = 'inProgress'")
            .IsUnique()
            .HasDatabaseName("ix_attempts_one_in_progress");

        builder.HasMany(e => e.Answers)
            .WithOne()
            .HasForeignKey(a => a.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(e => e.Answers)
            .HasField("_answers")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .AutoInclude();

        builder.HasMany(e => e.ItemResults)
            .WithOne()
            .HasForeignKey(r => r.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(e => e.ItemResults)
            .HasField("_itemResults")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .AutoInclude();
    }
}
