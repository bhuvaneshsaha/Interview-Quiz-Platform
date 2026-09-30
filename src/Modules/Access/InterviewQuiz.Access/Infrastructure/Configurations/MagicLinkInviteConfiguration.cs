using InterviewQuiz.Access.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewQuiz.Access.Infrastructure.Configurations;

public sealed class MagicLinkInviteConfiguration : IEntityTypeConfiguration<MagicLinkInvite>
{
    public void Configure(EntityTypeBuilder<MagicLinkInvite> builder)
    {
        builder.ToTable("magic_link_invites");
        builder.HasKey(invite => invite.Id);
        builder.Property(invite => invite.AssignmentId).IsRequired();
        builder.Property(invite => invite.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(invite => invite.TokenHash).IsUnique();
        builder.HasIndex(invite => invite.AssignmentId)
            .IsUnique()
            .HasFilter("\"RevokedAt\" IS NULL")
            .HasDatabaseName("ix_magic_link_invites_assignment_current");
        builder.Ignore(invite => invite.IsRevoked);
    }
}
