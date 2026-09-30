using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;

namespace InterviewQuiz.Catalog.Domain;

public sealed class Template
{
    private Template()
    {
    }

    public Guid Id { get; private set; }
    public Guid OriginQuizId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Template Create(Guid originQuizId, IClock clock, Guid? id = null)
    {
        if (originQuizId == Guid.Empty)
        {
            throw new DomainException("Origin quiz is required.");
        }

        var now = clock.UtcNow;
        return new Template
        {
            Id = id ?? Guid.NewGuid(),
            OriginQuizId = originQuizId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Touch(IClock clock)
    {
        UpdatedAtUtc = clock.UtcNow;
    }
}
