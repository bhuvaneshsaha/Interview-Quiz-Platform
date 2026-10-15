using InterviewQuiz.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewQuiz.Catalog.Infrastructure.Persistence.Configurations;

public sealed class AiDraftConfiguration : IEntityTypeConfiguration<AiDraft>
{
    public void Configure(EntityTypeBuilder<AiDraft> builder)
    {
        builder.ToTable("ai_drafts");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.OpeningId).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(Quiz.TitleMaxLength).IsRequired();
        builder.Property(e => e.ExpectedExperienceYears).IsRequired();
        builder.Property(e => e.ResumeText).HasMaxLength(AiDraft.ResumeMaxLength);
        builder.Property(e => e.RulesText).HasMaxLength(AiDraft.RulesMaxLength);
        builder.Property(e => e.GeneratedQuestionsJson)
            .HasColumnName("generated_questions_json")
            .HasColumnType("text")
            .IsRequired();
        builder.Property(e => e.Model).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.AcceptedByUserId).HasMaxLength(450);

        builder.HasIndex(e => e.CreatedByUserId);
        builder.HasIndex(e => e.OpeningId);
    }
}
