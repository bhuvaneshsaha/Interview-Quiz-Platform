using InterviewQuiz.Delivery.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewQuiz.Delivery.Infrastructure.Persistence.Configurations;

public sealed class LiveSessionEventConfiguration : IEntityTypeConfiguration<LiveSessionEvent>
{
    public void Configure(EntityTypeBuilder<LiveSessionEvent> builder)
    {
        builder.ToTable("live_session_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.AssignmentId).IsRequired();
        builder.Property(e => e.AttemptId);
        builder.Property(e => e.Action).HasMaxLength(LiveSessionEvent.ActionMaxLength).IsRequired();
        builder.Property(e => e.ActorUserId).HasMaxLength(LiveSessionEvent.ActorMaxLength);
        builder.Property(e => e.AtUtc).IsRequired();
        builder.Property(e => e.CorrelationId).HasMaxLength(LiveSessionEvent.CorrelationMaxLength).IsRequired();
        builder.Property(e => e.Detail).HasMaxLength(LiveSessionEvent.DetailMaxLength);
        builder.HasIndex(e => e.AssignmentId);
        builder.HasIndex(e => new { e.AssignmentId, e.AtUtc });
    }
}
