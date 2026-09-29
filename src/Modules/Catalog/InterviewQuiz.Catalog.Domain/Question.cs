using System.Text.Json;
using InterviewQuiz.Kernel.Exceptions;

namespace InterviewQuiz.Catalog.Domain;

public sealed class Question
{
    public const int StemMaxLength = 8000;

    private Question()
    {
        Body = JsonDocument.Parse("{}");
    }

    public Guid Id { get; private set; }
    public int SortOrder { get; private set; }
    public QuestionType Type { get; private set; }
    public string Stem { get; private set; } = null!;
    public ScoringMode ScoringMode { get; private set; }
    public CreditMode? CreditMode { get; private set; }
    public int Points { get; private set; }
    public JsonDocument Body { get; private set; }

    public static Question Create(
        Guid? id,
        int sortOrder,
        QuestionType type,
        string stem,
        ScoringMode? scoringMode,
        CreditMode? creditMode,
        int points,
        JsonElement body)
    {
        if (!Enum.IsDefined(type))
        {
            throw new DomainException("Unknown question type.");
        }

        if (string.IsNullOrWhiteSpace(stem))
        {
            throw new DomainException("Stem is required.");
        }

        var trimmedStem = stem.Trim();
        if (trimmedStem.Length > StemMaxLength)
        {
            throw new DomainException($"Stem cannot exceed {StemMaxLength} characters.");
        }

        if (points < 1)
        {
            throw new DomainException("Points must be greater than 0.");
        }

        if (sortOrder < 0)
        {
            throw new DomainException("Sort order cannot be negative.");
        }

        var resolvedScoring = scoringMode ?? QuestionBodyValidator.DefaultScoring(type, body);
        if (!Enum.IsDefined(resolvedScoring))
        {
            throw new DomainException("Unknown scoring mode.");
        }

        var requiresCredit = type is QuestionType.MultipleChoiceMulti or QuestionType.Ordering;
        if (requiresCredit)
        {
            if (creditMode is null)
            {
                throw new DomainException(
                    $"Credit mode is required for {CatalogJson.ToCamelCase(type)} questions.");
            }

            if (!Enum.IsDefined(creditMode.Value))
            {
                throw new DomainException("Unknown credit mode.");
            }
        }
        else if (creditMode is not null)
        {
            throw new DomainException(
                $"Credit mode must be omitted for {CatalogJson.ToCamelCase(type)} questions.");
        }

        var canonicalBody = QuestionBodyValidator.Normalize(type, body, resolvedScoring);

        return new Question
        {
            Id = id is null || id == Guid.Empty ? Guid.NewGuid() : id.Value,
            SortOrder = sortOrder,
            Type = type,
            Stem = trimmedStem,
            ScoringMode = resolvedScoring,
            CreditMode = requiresCredit ? creditMode : null,
            Points = points,
            Body = canonicalBody
        };
    }
}
