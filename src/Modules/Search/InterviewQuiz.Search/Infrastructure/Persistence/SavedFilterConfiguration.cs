using System.Text.Json;
using InterviewQuiz.Search.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewQuiz.Search.Infrastructure.Persistence;

public sealed class SavedFilterConfiguration : IEntityTypeConfiguration<SavedFilter>
{
    public void Configure(EntityTypeBuilder<SavedFilter> builder)
    {
        builder.ToTable("filters");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).HasMaxLength(SavedFilter.NameMaxLength).IsRequired();
        builder.Property(e => e.OwnerUserId).HasMaxLength(SavedFilter.OwnerUserIdMaxLength).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();

        builder.Property(e => e.Target)
            .HasConversion(CamelCaseEnumConverter.For<FilterTarget>())
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.ShareMode)
            .HasConversion(CamelCaseEnumConverter.For<FilterShareMode>())
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.Criteria)
            .HasColumnType("jsonb")
            .HasConversion(
                document => document.RootElement.GetRawText(),
                json => JsonDocument.Parse(json))
            .Metadata.SetValueComparer(new ValueComparer<JsonDocument>(
                (left, right) => left != null && right != null
                    && left.RootElement.GetRawText() == right.RootElement.GetRawText(),
                document => document.RootElement.GetRawText().GetHashCode(),
                document => JsonDocument.Parse(document.RootElement.GetRawText())));

        builder.Property(e => e.SharedWithUserIds)
            .HasColumnType("text[]")
            .IsRequired();

        builder.HasIndex(e => e.OwnerUserId);
        builder.HasIndex(e => e.Target);
        builder.HasIndex(e => e.UpdatedAtUtc);
    }
}
