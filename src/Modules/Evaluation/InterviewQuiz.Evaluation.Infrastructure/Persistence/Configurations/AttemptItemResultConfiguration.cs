using InterviewQuiz.Evaluation.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewQuiz.Evaluation.Infrastructure.Persistence.Configurations;

public sealed class AttemptItemResultConfiguration : IEntityTypeConfiguration<AttemptItemResult>
{
    public void Configure(EntityTypeBuilder<AttemptItemResult> builder)
    {
        builder.ToTable("attempt_item_results");
        builder.HasKey(e => new { e.AttemptId, e.QuestionId });
        builder.Property(e => e.AttemptId).IsRequired();
        builder.Property(e => e.QuestionId).IsRequired();
        builder.Property(e => e.SortOrder).IsRequired();
        builder.Property(e => e.Type).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ScoringMode).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Points).IsRequired();
        builder.Property(e => e.PointsAwarded).HasPrecision(18, 4);
        builder.Property(e => e.ReviewedByUserId).HasMaxLength(450);
        builder.Property(e => e.ReviewedAtUtc);
        builder.Property(e => e.ReviewComment).HasMaxLength(Attempt.ReviewCommentMaxLength);
        builder.Property(e => e.SuggestedPoints).HasPrecision(18, 4);
        builder.Property(e => e.SuggestionFeedback).HasMaxLength(Attempt.SuggestionFeedbackMaxLength);
        builder.Property(e => e.SuggestedByUserId).HasMaxLength(450);
        builder.Property(e => e.SuggestionModel).HasMaxLength(Attempt.SuggestionModelMaxLength);
        builder.Property(e => e.Status)
            .HasConversion(CamelCaseEnumConverter.For<ItemScoreStatus>())
            .HasMaxLength(32)
            .IsRequired();
    }
}
