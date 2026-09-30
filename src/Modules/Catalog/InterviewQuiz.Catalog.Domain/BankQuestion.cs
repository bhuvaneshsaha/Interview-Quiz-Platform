using System.Text.Json;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Tags;

namespace InterviewQuiz.Catalog.Domain;

/// <summary>
/// Company question-bank item. Not owned by a quiz. Not the drag-drop shared-bank body.
/// Payload rules reuse <see cref="Question.Create"/>; rows do not store <c>sourceQuestionId</c>.
/// </summary>
public sealed class BankQuestion
{
    public const int TitleMaxLength = Quiz.TitleMaxLength;
    public const int ExperienceYearsMax = 80;

    private BankQuestion()
    {
        Body = JsonDocument.Parse("{}");
    }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = null!;
    public Dictionary<string, string> Tags { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public int ExpectedExperienceYears { get; private set; }
    public QuestionType Type { get; private set; }
    public string Stem { get; private set; } = null!;
    public ScoringMode ScoringMode { get; private set; }
    public CreditMode? CreditMode { get; private set; }
    public int Points { get; private set; }
    public JsonDocument Body { get; private set; }
    public uint RowVersion { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    public bool IsArchived => ArchivedAtUtc is not null;

    public static BankQuestion Create(
        string title,
        IReadOnlyDictionary<string, string>? tags,
        int expectedExperienceYears,
        QuestionType type,
        string stem,
        ScoringMode? scoringMode,
        CreditMode? creditMode,
        int points,
        JsonElement body,
        IClock clock,
        Guid? id = null)
    {
        var payload = Question.Create(
            id,
            sortOrder: 0,
            type,
            stem,
            scoringMode,
            creditMode,
            points,
            body,
            sourceQuestionId: null);

        var now = clock.UtcNow;
        return new BankQuestion
        {
            Id = payload.Id,
            Title = RequireTitle(title),
            Tags = NormalizeTags(tags),
            ExpectedExperienceYears = RequireExperience(expectedExperienceYears),
            Type = payload.Type,
            Stem = payload.Stem,
            ScoringMode = payload.ScoringMode,
            CreditMode = payload.CreditMode,
            Points = payload.Points,
            Body = payload.Body,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Update(
        string title,
        IReadOnlyDictionary<string, string>? tags,
        int expectedExperienceYears,
        QuestionType type,
        string stem,
        ScoringMode? scoringMode,
        CreditMode? creditMode,
        int points,
        JsonElement body,
        IClock clock)
    {
        var payload = Question.Create(
            Id,
            sortOrder: 0,
            type,
            stem,
            scoringMode,
            creditMode,
            points,
            body,
            sourceQuestionId: null);

        Title = RequireTitle(title);
        Tags = NormalizeTags(tags);
        ExpectedExperienceYears = RequireExperience(expectedExperienceYears);
        Type = payload.Type;
        Stem = payload.Stem;
        ScoringMode = payload.ScoringMode;
        CreditMode = payload.CreditMode;
        Points = payload.Points;
        Body = payload.Body;
        UpdatedAtUtc = clock.UtcNow;
        RowVersion++;
    }

    public void Archive(IClock clock)
    {
        if (ArchivedAtUtc is not null)
        {
            throw new DomainException("Question is already archived.");
        }

        var now = clock.UtcNow;
        ArchivedAtUtc = now;
        UpdatedAtUtc = now;
        RowVersion++;
    }

    public void Unarchive(IClock clock)
    {
        if (ArchivedAtUtc is null)
        {
            throw new DomainException("Question is not archived.");
        }

        ArchivedAtUtc = null;
        UpdatedAtUtc = clock.UtcNow;
        RowVersion++;
    }

    private static string RequireTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("Title is required.");
        }

        var trimmed = title.Trim();
        if (trimmed.Length > TitleMaxLength)
        {
            throw new DomainException($"Title cannot exceed {TitleMaxLength} characters.");
        }

        return trimmed;
    }

    private static int RequireExperience(int years)
    {
        if (years is < 0 or > ExperienceYearsMax)
        {
            throw new DomainException($"Expected experience must be between 0 and {ExperienceYearsMax} years.");
        }

        return years;
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
