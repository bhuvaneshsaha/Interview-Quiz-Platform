using InterviewQuiz.Access.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewQuiz.Access.Infrastructure.Configurations;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ActorUserId).HasMaxLength(AuditEvent.ActorMaxLength).IsRequired();
        builder.Property(e => e.Action).HasMaxLength(AuditEvent.ActionMaxLength).IsRequired();
        builder.Property(e => e.TargetUserId).HasMaxLength(AuditEvent.TargetMaxLength).IsRequired();
        builder.Property(e => e.OccurredAtUtc).IsRequired();
        builder.Property(e => e.CorrelationId).HasMaxLength(AuditEvent.CorrelationMaxLength).IsRequired();
        builder.HasIndex(e => e.OccurredAtUtc);
        builder.HasIndex(e => new { e.Action, e.TargetUserId });
    }
}
