using System.Text.Json;

namespace InterviewQuiz.Delivery.Domain;

/// <summary>
/// Frozen quiz graph copied at assign time. Never updated. Question ids in
/// <see cref="Payload"/> are the source quiz question ids.
/// </summary>
public sealed class AssignmentSnapshot
{
    private AssignmentSnapshot()
    {
        Payload = JsonDocument.Parse("{}");
    }

    public Guid Id { get; private set; }
    public Guid AssignmentId { get; private set; }
    public string Title { get; private set; } = "";
    public int QuestionCount { get; private set; }
    public JsonDocument Payload { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static AssignmentSnapshot Freeze(
        Guid id,
        Guid assignmentId,
        string title,
        int questionCount,
        JsonDocument payload,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (questionCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(questionCount));
        }

        return new AssignmentSnapshot
        {
            Id = id,
            AssignmentId = assignmentId,
            Title = title ?? "",
            QuestionCount = questionCount,
            Payload = payload,
            CreatedAtUtc = createdAtUtc
        };
    }
}
