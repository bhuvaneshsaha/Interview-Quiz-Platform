using InterviewQuiz.Openings.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewQuiz.Openings.Infrastructure.Persistence.Configurations;

public sealed class OpeningFieldDefinitionConfiguration : IEntityTypeConfiguration<OpeningFieldDefinition>
{
    public void Configure(EntityTypeBuilder<OpeningFieldDefinition> builder)
    {
        builder.ToTable("opening_field_definitions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Key).HasMaxLength(OpeningFieldDefinition.KeyMaxLength).IsRequired();
        builder.Property(e => e.DisplayName).HasMaxLength(OpeningFieldDefinition.DisplayNameMaxLength).IsRequired();
        builder.Property(e => e.SortOrder).IsRequired();
        builder.HasIndex(e => e.Key).IsUnique();
    }
}
