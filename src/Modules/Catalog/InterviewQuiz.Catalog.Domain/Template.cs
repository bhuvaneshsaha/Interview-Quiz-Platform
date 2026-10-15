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
    public uint RowVersion { get; private set; }
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
            RowVersion = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void EnsureRowVersion(uint expected)
    {
        if (RowVersion != expected)
        {
            throw new ConcurrencyException("Template was modified by another request. Reload and retry.");
        }
    }

    public void Touch(IClock clock)
    {
        UpdatedAtUtc = clock.UtcNow;
        RowVersion++;
    }
}
