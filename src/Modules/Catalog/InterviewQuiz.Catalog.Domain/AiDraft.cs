using InterviewQuiz.Kernel.Exceptions;

namespace InterviewQuiz.Catalog.Domain;

public sealed class AiDraft
{
    public const int ResumeMaxLength = 8000;
    public const int RulesMaxLength = 4000;
    public const string StatusGenerated = "generated";
    public const string StatusAccepted = "accepted";
    public const string UnavailableMessage = "AI drafts are not configured.";
    public const string FailedMessage = "AI draft could not be produced.";
    public const string EditRequiredMessage = "Edit the draft before saving it as a quiz.";
    public const string AlreadyAcceptedMessage = "This draft was already saved as a quiz.";
    public const string TemplatePairMessage = "Template id and version id are both required.";
    public const string ResumeTooLongMessage = "Resume cannot exceed 8000 characters.";
    public const string RulesTooLongMessage = "Rules cannot exceed 4000 characters.";

    private AiDraft()
    {
    }

    public Guid Id { get; private set; }
    public Guid OpeningId { get; private set; }
    public string Title { get; private set; } = "";
    public int ExpectedExperienceYears { get; private set; }
    public string? ResumeText { get; private set; }
    public string? RulesText { get; private set; }
    public Guid? TemplateId { get; private set; }
    public Guid? TemplateVersionId { get; private set; }
    public string GeneratedQuestionsJson { get; private set; } = "[]";
    public string Model { get; private set; } = "";
    public string Status { get; private set; } = StatusGenerated;
    public string CreatedByUserId { get; private set; } = "";
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public Guid? AcceptedQuizId { get; private set; }
    public string? AcceptedByUserId { get; private set; }
    public DateTimeOffset? AcceptedAtUtc { get; private set; }

    public static AiDraft Create(
        Guid openingId,
        string title,
        int expectedExperienceYears,
        string? resumeText,
        string? rulesText,
        Guid? templateId,
        Guid? templateVersionId,
        string generatedQuestionsJson,
        string model,
        string createdByUserId,
        DateTimeOffset createdAtUtc)
    {
        return new AiDraft
        {
            Id = Guid.NewGuid(),
            OpeningId = openingId,
            Title = title,
            ExpectedExperienceYears = expectedExperienceYears,
            ResumeText = resumeText,
            RulesText = rulesText,
            TemplateId = templateId,
            TemplateVersionId = templateVersionId,
            GeneratedQuestionsJson = generatedQuestionsJson,
            Model = model,
            Status = StatusGenerated,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = createdAtUtc
        };
    }

    public void MarkAccepted(Guid quizId, string userId, DateTimeOffset utcNow)
    {
        if (Status == StatusAccepted)
        {
            throw new ConflictException(AlreadyAcceptedMessage);
        }

        if (quizId == Guid.Empty)
        {
            throw new DomainException("Quiz id is required.");
        }

        AcceptedQuizId = quizId;
        AcceptedByUserId = userId;
        AcceptedAtUtc = utcNow;
        Status = StatusAccepted;
    }
}
