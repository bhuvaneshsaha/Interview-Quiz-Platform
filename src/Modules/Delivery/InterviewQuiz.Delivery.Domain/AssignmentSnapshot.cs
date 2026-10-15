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

    /// <summary>
    /// Catalog quiz <c>rowVersion</c> at assign time. Later edits increment the live quiz
    /// and leave this stamp, and the payload, unchanged.
    /// </summary>
    public long QuizRowVersion { get; private set; }

    public static AssignmentSnapshot Freeze(
        Guid id,
        Guid assignmentId,
        string title,
        int questionCount,
        JsonDocument payload,
        DateTimeOffset createdAtUtc,
        long quizRowVersion)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (questionCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(questionCount));
        }

        if (quizRowVersion < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quizRowVersion));
        }

        return new AssignmentSnapshot
        {
            Id = id,
            AssignmentId = assignmentId,
            Title = title ?? "",
            QuestionCount = questionCount,
            Payload = payload,
            CreatedAtUtc = createdAtUtc,
            QuizRowVersion = quizRowVersion
        };
    }
}
