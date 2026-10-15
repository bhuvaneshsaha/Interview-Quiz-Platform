using InterviewQuiz.Evaluation.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewQuiz.Evaluation.Infrastructure.Persistence.Configurations;

public sealed class AttemptAnswerConfiguration : IEntityTypeConfiguration<AttemptAnswer>
{
    public void Configure(EntityTypeBuilder<AttemptAnswer> builder)
    {
        builder.ToTable("attempt_answers");
        builder.HasKey(e => new { e.AttemptId, e.QuestionId });
        builder.Property(e => e.AttemptId).IsRequired();
        builder.Property(e => e.QuestionId).IsRequired();
        builder.Property(e => e.Value)
            .HasColumnType("jsonb")
            .HasConversion(JsonDocumentConverter.Converter)
            .Metadata.SetValueComparer(JsonDocumentConverter.Comparer);
    }
}
