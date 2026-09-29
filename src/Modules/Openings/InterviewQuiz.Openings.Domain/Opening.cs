using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Tags;

namespace InterviewQuiz.Openings.Domain;

public sealed class Opening
{
    public const int TitleMaxLength = 200;
    public const int OwnerMaxLength = 256;
    public const int HandlerMaxLength = 256;
    public const int JobDescriptionMaxLength = 32_000;

    private Opening()
    {
    }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = null!;
    public string JobDescription { get; private set; } = "";
    public string Owner { get; private set; } = null!;
    public DateOnly StartDate { get; private set; }
    public DateOnly? ExpectedCloseDate { get; private set; }
    public int Headcount { get; private set; }
    public int ExpectedExperienceYears { get; private set; }
    public List<string> Handlers { get; private set; } = [];
    public Dictionary<string, string> Tags { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public uint RowVersion { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Opening Create(
        string title,
        string? jobDescription,
        string owner,
        DateOnly startDate,
        DateOnly? expectedCloseDate,
        int headcount,
        int expectedExperienceYears,
        IEnumerable<string>? handlers,
        IReadOnlyDictionary<string, string>? tags,
        IClock clock,
        Guid? id = null)
    {
        var now = clock.UtcNow;
        var opening = new Opening
        {
            Id = id ?? Guid.NewGuid(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        opening.Apply(
            title,
            jobDescription,
            owner,
            startDate,
            expectedCloseDate,
            headcount,
            expectedExperienceYears,
            handlers,
            tags,
            now);

        return opening;
    }

    private void Apply(
        string title,
        string? jobDescription,
        string owner,
        DateOnly startDate,
        DateOnly? expectedCloseDate,
        int headcount,
        int expectedExperienceYears,
        IEnumerable<string>? handlers,
        IReadOnlyDictionary<string, string>? tags,
        DateTimeOffset now)
    {
        Title = RequireText(title, "Title", TitleMaxLength);
        JobDescription = NormalizeOptional(jobDescription, "Job description", JobDescriptionMaxLength);
        Owner = RequireText(owner, "Owner", OwnerMaxLength);

        if (expectedCloseDate is { } close && close < startDate)
        {
            throw new DomainException("Expected close date cannot be earlier than the start date.");
        }

        if (headcount < 1)
        {
            throw new DomainException("Headcount must be at least 1.");
        }

        if (expectedExperienceYears < 0)
        {
            throw new DomainException("Expected experience cannot be negative.");
        }

        StartDate = startDate;
        ExpectedCloseDate = expectedCloseDate;
        Headcount = headcount;
        ExpectedExperienceYears = expectedExperienceYears;
        Handlers = NormalizeHandlers(handlers);
        Tags = NormalizeTags(tags);
        UpdatedAtUtc = now;
    }

    public void Update(
        string title,
        string? jobDescription,
        string owner,
        DateOnly startDate,
        DateOnly? expectedCloseDate,
        int headcount,
        int expectedExperienceYears,
        IEnumerable<string>? handlers,
        IReadOnlyDictionary<string, string>? tags,
        IClock clock)
    {
        Apply(
            title,
            jobDescription,
            owner,
            startDate,
            expectedCloseDate,
            headcount,
            expectedExperienceYears,
            handlers,
            tags,
            clock.UtcNow);
        RowVersion++;
    }

    private static string RequireText(string value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{name} is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{name} cannot exceed {maxLength} characters.");
        }

        return trimmed;
    }

    private static string NormalizeOptional(string? value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{name} cannot exceed {maxLength} characters.");
        }

        return trimmed;
    }

    private static List<string> NormalizeHandlers(IEnumerable<string>? handlers)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var handler in handlers ?? [])
        {
            if (string.IsNullOrWhiteSpace(handler))
            {
                throw new DomainException("Handler identity cannot be empty.");
            }

            var trimmed = handler.Trim();
            if (trimmed.Length > HandlerMaxLength)
            {
                throw new DomainException($"Handler identity cannot exceed {HandlerMaxLength} characters.");
            }

            if (seen.Add(trimmed))
            {
                result.Add(trimmed);
            }
        }

        return result;
    }

    private static Dictionary<string, string> NormalizeTags(IReadOnlyDictionary<string, string>? tags)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in tags ?? new Dictionary<string, string>())
        {
            var tag = new Tag(pair.Key, pair.Value);
            if (!result.TryAdd(tag.Key, tag.Value))
            {
                throw new DomainException($"Duplicate tag key '{tag.Key}'.");
            }
        }

        return result;
    }
}
