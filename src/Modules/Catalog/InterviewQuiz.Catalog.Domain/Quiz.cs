using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Tags;

namespace InterviewQuiz.Catalog.Domain;

public sealed class Quiz
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 8000;

    private Quiz()
    {
    }

    public Guid Id { get; private set; }
    public Guid OpeningId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = "";
    public int ExpectedExperienceYears { get; private set; }
    public Dictionary<string, string> Tags { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<Question> Questions { get; private set; } = [];
    public Guid? OriginTemplateId { get; private set; }
    public Guid? SourceTemplateVersionId { get; private set; }
    public uint RowVersion { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Quiz Create(
        Guid openingId,
        string title,
        string? description,
        int expectedExperienceYears,
        IReadOnlyDictionary<string, string>? tags,
        IReadOnlyList<Question> questions,
        IClock clock,
        Guid? id = null,
        Guid? originTemplateId = null,
        Guid? sourceTemplateVersionId = null)
    {
        var now = clock.UtcNow;
        var quiz = new Quiz
        {
            Id = id ?? Guid.NewGuid(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            OriginTemplateId = originTemplateId is null || originTemplateId == Guid.Empty
                ? null
                : originTemplateId,
            SourceTemplateVersionId = sourceTemplateVersionId is null || sourceTemplateVersionId == Guid.Empty
                ? null
                : sourceTemplateVersionId
        };

        quiz.Apply(openingId, title, description, expectedExperienceYears, tags, questions, now);
        return quiz;
    }

    /// <summary>
    /// Server-owned lineage. Increments <see cref="RowVersion"/> when the origin actually changes.
    /// </summary>
    public void AttachToTemplate(Guid templateId, IClock clock)
    {
        if (templateId == Guid.Empty)
        {
            throw new DomainException("Template is required.");
        }

        if (OriginTemplateId == templateId)
        {
            return;
        }

        OriginTemplateId = templateId;
        UpdatedAtUtc = clock.UtcNow;
        RowVersion++;
    }

    public void Update(
        Guid openingId,
        string title,
        string? description,
        int expectedExperienceYears,
        IReadOnlyDictionary<string, string>? tags,
        IReadOnlyList<Question> questions,
        IClock clock)
    {
        Apply(openingId, title, description, expectedExperienceYears, tags, questions, clock.UtcNow);
        RowVersion++;
    }

    /// <summary>
    /// Copy-on-include: insert bank copies (already given new ids and <c>sourceQuestionId</c>)
    /// at <paramref name="insertAt"/> (clamped 0..count; null appends), then re-number sortOrder.
    /// Adds/removes tracked questions in place so EF can insert owned rows without a full replace.
    /// </summary>
    public void IncludeQuestions(IReadOnlyList<Question> copies, int? insertAt, IClock clock)
    {
        if (copies is null || copies.Count == 0)
        {
            throw new DomainException("Question ids are required.");
        }

        var ordered = Questions
            .OrderBy(q => q.SortOrder)
            .ThenBy(q => q.Id)
            .ToList();

        var index = Math.Clamp(insertAt ?? ordered.Count, 0, ordered.Count);
        var after = ordered.Skip(index).ToList();
        var nextOrder = index;

        foreach (var copy in copies)
        {
            Questions.Add(WithSortOrder(copy, nextOrder++));
        }

        foreach (var existing in after)
        {
            var newOrder = nextOrder++;
            if (existing.SortOrder == newOrder)
            {
                continue;
            }

            Questions.Remove(existing);
            Questions.Add(WithSortOrder(existing, newOrder));
        }

        var seen = new HashSet<Guid>();
        foreach (var question in Questions)
        {
            if (!seen.Add(question.Id))
            {
                throw new DomainException($"Duplicate question id '{question.Id}'.");
            }
        }

        UpdatedAtUtc = clock.UtcNow;
        RowVersion++;
    }

    private static Question WithSortOrder(Question question, int sortOrder)
        => Question.Create(
            question.Id,
            sortOrder,
            question.Type,
            question.Stem,
            question.ScoringMode,
            question.CreditMode,
            question.Points,
            question.Body.RootElement.Clone(),
            question.SourceQuestionId);

    private void Apply(
        Guid openingId,
        string title,
        string? description,
        int expectedExperienceYears,
        IReadOnlyDictionary<string, string>? tags,
        IReadOnlyList<Question> questions,
        DateTimeOffset now)
    {
        if (openingId == Guid.Empty)
        {
            throw new DomainException("Opening is required.");
        }

        Title = RequireText(title, "Title", TitleMaxLength);
        Description = NormalizeOptional(description, "Description", DescriptionMaxLength);

        if (expectedExperienceYears < 0)
        {
            throw new DomainException("Expected experience cannot be negative.");
        }

        OpeningId = openingId;
        ExpectedExperienceYears = expectedExperienceYears;
        Tags = NormalizeTags(tags);
        ReplaceQuestions(questions);
        UpdatedAtUtc = now;
    }

    private void ReplaceQuestions(IReadOnlyList<Question> questions)
    {
        var list = (questions ?? []).ToList();
        var seen = new HashSet<Guid>();
        foreach (var question in list)
        {
            if (!seen.Add(question.Id))
            {
                throw new DomainException($"Duplicate question id '{question.Id}'.");
            }
        }

        Questions.Clear();
        Questions.AddRange(list);
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
