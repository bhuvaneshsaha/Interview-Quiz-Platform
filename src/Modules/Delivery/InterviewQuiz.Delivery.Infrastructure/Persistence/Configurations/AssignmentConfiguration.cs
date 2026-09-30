using InterviewQuiz.Delivery.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewQuiz.Delivery.Infrastructure.Persistence.Configurations;

public sealed class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.ToTable("assignments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.OpeningId).IsRequired();
        builder.Property(e => e.QuizId).IsRequired();
        builder.Property(e => e.SnapshotId).IsRequired();
        builder.Property(e => e.CandidateEmail).HasMaxLength(Assignment.EmailMaxLength).IsRequired();
        builder.Property(e => e.OverallDurationMinutes);
        builder.Property(e => e.AttemptLimit).IsRequired();
        builder.Property(e => e.CreatedByUserId).HasMaxLength(64).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();

        builder.Property(e => e.Mode)
            .HasConversion(CamelCaseEnumConverter.For<AssignmentMode>())
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion(CamelCaseEnumConverter.For<AssignmentStatus>())
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.RowVersion)
            .HasColumnName("row_version")
            .IsConcurrencyToken();

        builder.HasIndex(e => e.OpeningId);
        builder.HasIndex(e => e.CreatedAtUtc);
        builder.HasIndex(e => e.CandidateEmail);
        builder.HasIndex(e => e.SnapshotId).IsUnique();

        builder.HasOne(e => e.Snapshot)
            .WithOne()
            .HasForeignKey<AssignmentSnapshot>(s => s.AssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.Snapshot).AutoInclude();
    }
}
