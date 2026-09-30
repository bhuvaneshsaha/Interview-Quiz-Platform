using InterviewQuiz.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
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

        builder.Property(e => e.OriginTemplateId);
        builder.Property(e => e.SourceTemplateVersionId);

        builder.HasIndex(e => e.OpeningId);
        builder.HasIndex(e => e.UpdatedAtUtc);
        builder.HasIndex(e => e.OriginTemplateId);
        builder.HasIndex(e => e.Tags).HasMethod("gin");

        builder.OwnsMany(e => e.Questions, question =>
        {
            QuestionOwnership.Configure(question, "questions", "QuizId");
        });

        builder.Navigation(e => e.Questions).AutoInclude();
    }
}
