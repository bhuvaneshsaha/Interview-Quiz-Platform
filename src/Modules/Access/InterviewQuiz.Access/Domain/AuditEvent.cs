namespace InterviewQuiz.Access.Domain;

/// <summary>
/// Minimal access audit row. Actor, target, time, and correlation id for Super Admin assignment changes.
/// </summary>
public sealed class AuditEvent
{
    public const int ActorMaxLength = 256;
    public const int ActionMaxLength = 64;
    public const int TargetMaxLength = 256;
    public const int CorrelationMaxLength = 128;

    public Guid Id { get; set; }

    public string ActorUserId { get; set; } = "";

    public string Action { get; set; } = "";

    public string TargetUserId { get; set; } = "";

    public DateTimeOffset OccurredAtUtc { get; set; }

    public string CorrelationId { get; set; } = "";
}
