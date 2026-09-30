using InterviewQuiz.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewQuiz.Catalog.Infrastructure.Persistence.Configurations;

public sealed class TemplateVersionConfiguration : IEntityTypeConfiguration<TemplateVersion>
{
    public void Configure(EntityTypeBuilder<TemplateVersion> builder)
    {
        builder.ToTable("template_versions");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TemplateId).IsRequired();
        builder.Property(e => e.VersionNumber).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(Quiz.TitleMaxLength).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(Quiz.DescriptionMaxLength).IsRequired();
        builder.Property(e => e.ExpectedExperienceYears).IsRequired();
        builder.Property(e => e.PublishedFromQuizId).IsRequired();
        builder.Property(e => e.PublishedAtUtc).IsRequired();
        builder.Property(e => e.PublishedByUserId)
            .HasMaxLength(TemplateVersion.PublishedByUserIdMaxLength)
            .IsRequired();

        builder.Property(e => e.Tags)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.HasIndex(e => new { e.TemplateId, e.VersionNumber }).IsUnique();
        builder.HasIndex(e => e.PublishedAtUtc);
        builder.HasIndex(e => e.Tags).HasMethod("gin");

        builder.HasOne<Template>()
            .WithMany()
            .HasForeignKey(e => e.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.OwnsMany(e => e.Questions, question =>
        {
            QuestionOwnership.Configure(question, "template_version_questions", "TemplateVersionId");
        });
    }
}
