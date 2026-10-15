using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Tags;

namespace InterviewQuiz.Catalog.Domain;

public sealed class TemplateVersion
{
    public const int PublishedByUserIdMaxLength = 256;
    public const int ChangeNoteMaxLength = 500;

    private TemplateVersion()
    {
    }

    public Guid Id { get; private set; }
    public Guid TemplateId { get; private set; }
    public int VersionNumber { get; private set; }
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = "";
    public int ExpectedExperienceYears { get; private set; }
    public Dictionary<string, string> Tags { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public Guid PublishedFromQuizId { get; private set; }
    public DateTimeOffset PublishedAtUtc { get; private set; }
    public string PublishedByUserId { get; private set; } = null!;
    public string ChangeNote { get; private set; } = "";

    /// <summary>
    /// When set, this version reuses that version's question rows and does not own copies.
    /// Null means this version owns <see cref="Questions"/>.
    /// </summary>
    public Guid? ContentVersionId { get; private set; }

    public List<Question> Questions { get; private set; } = [];

    private List<Question> _sharedQuestions = [];

    public static TemplateVersion FromQuiz(
        Guid templateId,
        int versionNumber,
        Quiz quiz,
        string publishedByUserId,
        IClock clock,
        Guid? id = null,
        IReadOnlyList<Question>? questions = null,
        string? changeNote = null)
    {
        var copies = questions
            ?? quiz.Questions
                .OrderBy(q => q.SortOrder)
                .ThenBy(q => q.Id)
                .Select((q, i) => q.CopyWithNewId(i))
                .ToList();

        return Create(
            templateId,
            versionNumber,
            quiz.Title,
            quiz.Description,
            quiz.ExpectedExperienceYears,
            quiz.Tags,
            quiz.Id,
            publishedByUserId,
            copies,
            clock,
            id,
            changeNote);
    }

    public static TemplateVersion Create(
        Guid templateId,
        int versionNumber,
        string title,
        string description,
        int expectedExperienceYears,
        IReadOnlyDictionary<string, string>? tags,
        Guid publishedFromQuizId,
        string publishedByUserId,
        IReadOnlyList<Question> questions,
        IClock clock,
        Guid? id = null,
        string? changeNote = null,
        Guid? contentVersionId = null)
    {
        if (templateId == Guid.Empty)
        {
            throw new DomainException("Template is required.");
        }

        if (versionNumber < 1)
        {
            throw new DomainException("Version number must be 1 or greater.");
        }

        if (publishedFromQuizId == Guid.Empty)
        {
            throw new DomainException("Published-from quiz is required.");
        }

        if (string.IsNullOrWhiteSpace(publishedByUserId))
        {
            throw new DomainException("Published-by user is required.");
        }

        var trimmedPublisher = publishedByUserId.Trim();
        if (trimmedPublisher.Length > PublishedByUserIdMaxLength)
        {
            throw new DomainException($"Published-by user cannot exceed {PublishedByUserIdMaxLength} characters.");
        }

        var trimmedTitle = RequireText(title, "Title", Quiz.TitleMaxLength);
        var trimmedDescription = NormalizeOptional(description, "Description", Quiz.DescriptionMaxLength);
        if (expectedExperienceYears < 0)
        {
            throw new DomainException("Expected experience cannot be negative.");
        }

        var list = (questions ?? []).ToList();
        if (contentVersionId is { } sharedId)
        {
            if (sharedId == Guid.Empty)
            {
                throw new DomainException("Content version is required.");
            }

            if (list.Count > 0)
            {
                throw new DomainException("A metadata version reuses question content and does not copy questions.");
            }
        }

        var seen = new HashSet<Guid>();
        foreach (var question in list)
        {
            if (!seen.Add(question.Id))
            {
                throw new DomainException($"Duplicate question id '{question.Id}'.");
            }
        }

        return new TemplateVersion
        {
            Id = id ?? Guid.NewGuid(),
            TemplateId = templateId,
            VersionNumber = versionNumber,
            Title = trimmedTitle,
            Description = trimmedDescription,
            ExpectedExperienceYears = expectedExperienceYears,
            Tags = NormalizeTags(tags),
            PublishedFromQuizId = publishedFromQuizId,
            PublishedAtUtc = clock.UtcNow,
            PublishedByUserId = trimmedPublisher,
            ChangeNote = NormalizeChangeNote(changeNote),
            ContentVersionId = contentVersionId,
            Questions = list
        };
    }

    /// <summary>
    /// Questions this version presents. Metadata versions reuse another version's rows.
    /// </summary>
    public IReadOnlyList<Question> ReadQuestions()
        => ContentVersionId is null ? Questions : _sharedQuestions;

    public int ReadQuestionCount() => ReadQuestions().Count;

    public void UseSharedQuestions(IReadOnlyList<Question> questions)
    {
        if (ContentVersionId is null)
        {
            throw new InvalidOperationException("This version owns its questions.");
        }

        _sharedQuestions = questions.ToList();
    }

    /// <summary>
    /// Version whose question rows this version presents. Owners point at themselves.
    /// </summary>
    public Guid QuestionContentVersionId => ContentVersionId ?? Id;

    private static string NormalizeChangeNote(string? changeNote)
    {
        if (string.IsNullOrWhiteSpace(changeNote))
        {
            return "";
        }

        var trimmed = changeNote.Trim();
        if (trimmed.Length > ChangeNoteMaxLength)
        {
            throw new DomainException($"Change note cannot exceed {ChangeNoteMaxLength} characters.");
        }

        return trimmed;
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
