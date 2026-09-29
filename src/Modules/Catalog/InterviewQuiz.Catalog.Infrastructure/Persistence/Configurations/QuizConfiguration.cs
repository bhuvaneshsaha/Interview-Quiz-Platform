using System.Text.Json;
using InterviewQuiz.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewQuiz.Catalog.Infrastructure.Persistence.Configurations;

public sealed class QuizConfiguration : IEntityTypeConfiguration<Quiz>
{
    public void Configure(EntityTypeBuilder<Quiz> builder)
    {
        builder.ToTable("quizzes");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.OpeningId).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(Quiz.TitleMaxLength).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(Quiz.DescriptionMaxLength).IsRequired();
        builder.Property(e => e.ExpectedExperienceYears).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();

        builder.Property(e => e.Tags)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.RowVersion)
            .HasColumnName("row_version")
            .IsConcurrencyToken();

        builder.HasIndex(e => e.OpeningId);
        builder.HasIndex(e => e.UpdatedAtUtc);
        builder.HasIndex(e => e.Tags).HasMethod("gin");

        builder.OwnsMany(e => e.Questions, question =>
        {
            question.ToTable("questions");
            question.WithOwner().HasForeignKey("QuizId");
            question.HasKey(e => e.Id);

            question.Property(e => e.SortOrder).IsRequired();
            question.Property(e => e.Stem).HasMaxLength(Question.StemMaxLength).IsRequired();
            question.Property(e => e.Points).IsRequired();

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

            question.HasIndex("QuizId", nameof(Question.SortOrder));
        });

        builder.Navigation(e => e.Questions).AutoInclude();
    }
}
