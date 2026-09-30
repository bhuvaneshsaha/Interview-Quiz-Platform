using System.Text.Json;
using InterviewQuiz.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewQuiz.Catalog.Infrastructure.Persistence.Configurations;

internal static class QuestionOwnership
{
    public static void Configure<TOwner>(
        OwnedNavigationBuilder<TOwner, Question> question,
        string tableName,
        string foreignKey)
        where TOwner : class
    {
        question.ToTable(tableName);
        question.WithOwner().HasForeignKey(foreignKey);
        question.HasKey(e => e.Id);

        question.Property(e => e.SortOrder).IsRequired();
        question.Property(e => e.Stem).HasMaxLength(Question.StemMaxLength).IsRequired();
        question.Property(e => e.Points).IsRequired();
        question.Property(e => e.SourceQuestionId);

        question.Property(e => e.Type)
            .HasConversion(CamelCaseEnumConverter.For<QuestionType>())
            .HasMaxLength(64)
            .IsRequired();

        question.Property(e => e.ScoringMode)
            .HasConversion(CamelCaseEnumConverter.For<ScoringMode>())
            .HasMaxLength(32)
            .IsRequired();

        question.Property(e => e.CreditMode)
            .HasConversion(CamelCaseEnumConverter.ForNullable<CreditMode>())
            .HasMaxLength(32);

        question.Property(e => e.Body)
            .HasColumnType("jsonb")
            .HasConversion(
                document => document.RootElement.GetRawText(),
                json => JsonDocument.Parse(json))
            .Metadata.SetValueComparer(new ValueComparer<JsonDocument>(
                (left, right) => left != null && right != null
                    && left.RootElement.GetRawText() == right.RootElement.GetRawText(),
                document => document.RootElement.GetRawText().GetHashCode(),
                document => JsonDocument.Parse(document.RootElement.GetRawText())));

        question.HasIndex(foreignKey, nameof(Question.SortOrder));
    }
}
