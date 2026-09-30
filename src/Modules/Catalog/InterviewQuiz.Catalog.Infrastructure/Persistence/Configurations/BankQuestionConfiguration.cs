using System.Text.Json;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewQuiz.Catalog.Infrastructure.Persistence.Configurations;

public sealed class BankQuestionConfiguration : IEntityTypeConfiguration<BankQuestion>
{
    public void Configure(EntityTypeBuilder<BankQuestion> builder)
    {
        builder.ToTable("bank_questions");
        builder.HasKey(e => e.Id);

        builder.Ignore(e => e.IsArchived);

        builder.Property(e => e.Title).HasMaxLength(BankQuestion.TitleMaxLength).IsRequired();
        builder.Property(e => e.ExpectedExperienceYears).IsRequired();
        builder.Property(e => e.Stem).HasMaxLength(Question.StemMaxLength).IsRequired();
        builder.Property(e => e.Points).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();
        builder.Property(e => e.ArchivedAtUtc);

        builder.Property(e => e.Tags)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.RowVersion)
            .HasColumnName("row_version")
            .IsConcurrencyToken();

        builder.Property(e => e.Type)
            .HasConversion(CamelCaseEnumConverter.For<QuestionType>())
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.ScoringMode)
            .HasConversion(CamelCaseEnumConverter.For<ScoringMode>())
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.CreditMode)
            .HasConversion(CamelCaseEnumConverter.ForNullable<CreditMode>())
            .HasMaxLength(32);

        builder.Property(e => e.Body)
            .HasColumnType("jsonb")
            .HasConversion(
                document => document.RootElement.GetRawText(),
                json => JsonDocument.Parse(json))
            .Metadata.SetValueComparer(new ValueComparer<JsonDocument>(
                (left, right) => left != null && right != null
                    && left.RootElement.GetRawText() == right.RootElement.GetRawText(),
                document => document.RootElement.GetRawText().GetHashCode(),
                document => JsonDocument.Parse(document.RootElement.GetRawText())));

        builder.HasIndex(e => e.UpdatedAtUtc);
        builder.HasIndex(e => e.ArchivedAtUtc);
        builder.HasIndex(e => e.Type);
        builder.HasIndex(e => e.Tags).HasMethod("gin");
    }
}
