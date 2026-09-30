using InterviewQuiz.Delivery.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewQuiz.Delivery.Infrastructure.Persistence.Configurations;

public sealed class AssignmentSnapshotConfiguration : IEntityTypeConfiguration<AssignmentSnapshot>
{
    public void Configure(EntityTypeBuilder<AssignmentSnapshot> builder)
    {
        builder.ToTable("assignment_snapshots");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.AssignmentId).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
        builder.Property(e => e.QuestionCount).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();

        builder.Property(e => e.Payload)
            .HasColumnType("jsonb")
            .HasConversion(JsonDocumentConverter.Converter)
            .Metadata.SetValueComparer(JsonDocumentConverter.Comparer);

        builder.HasIndex(e => e.AssignmentId).IsUnique();
        builder.HasIndex(e => e.Title);
    }
}
