namespace InterviewQuiz.Delivery.Domain;

/// <summary>
/// Append-only host and system audit row for a live session. No answers, stems, or tokens.
/// </summary>
public sealed class LiveSessionEvent
{
    public const int ActionMaxLength = 32;
    public const int ActorMaxLength = 256;
    public const int CorrelationMaxLength = 128;
    public const int DetailMaxLength = 500;

    private LiveSessionEvent()
    {
    }

    public Guid Id { get; private set; }
    public Guid AssignmentId { get; private set; }
    public Guid? AttemptId { get; private set; }
    public string Action { get; private set; } = "";
    public string? ActorUserId { get; private set; }
    public DateTimeOffset AtUtc { get; private set; }
    public string CorrelationId { get; private set; } = "";
    public string? Detail { get; private set; }

    public static LiveSessionEvent Create(
        Guid assignmentId,
        string action,
        DateTimeOffset atUtc,
        string correlationId,
        Guid? attemptId = null,
        string? actorUserId = null,
        string? detail = null)
    {
        if (assignmentId == Guid.Empty)
        {
            throw new ArgumentException("Assignment id is required.", nameof(assignmentId));
        }

        return new LiveSessionEvent
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignmentId,
            AttemptId = attemptId is { } id && id != Guid.Empty ? id : null,
            Action = TrimRequired(action, ActionMaxLength),
            ActorUserId = TrimOptional(actorUserId, ActorMaxLength),
            AtUtc = atUtc,
            CorrelationId = TrimOptional(correlationId, CorrelationMaxLength) ?? "unknown",
            Detail = TrimOptional(detail, DetailMaxLength)
        };
    }

    private static string TrimRequired(string? value, int max)
    {
        var trimmed = value?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Action is required.", nameof(value));
        }

        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    private static string? TrimOptional(string? value, int max)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
