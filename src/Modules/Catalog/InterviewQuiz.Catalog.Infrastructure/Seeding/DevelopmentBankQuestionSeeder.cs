using System.Text.Json;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Catalog.Infrastructure.Persistence;
using InterviewQuiz.Kernel.Clock;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InterviewQuiz.Catalog.Infrastructure.Seeding;

public sealed class DevelopmentBankQuestionSeeder
{
    public static readonly Guid SampleBankMcSingle = Guid.Parse("6d0f4a43-9e5a-4f2d-ab44-3c1f5e9d4001");
    public static readonly Guid SampleBankTrueFalse = Guid.Parse("6d0f4a43-9e5a-4f2d-ab44-3c1f5e9d4002");
    public static readonly Guid SampleBankShortText = Guid.Parse("6d0f4a43-9e5a-4f2d-ab44-3c1f5e9d4003");

    private readonly CatalogDbContext _db;
    private readonly IClock _clock;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DevelopmentBankQuestionSeeder> _logger;

    public DevelopmentBankQuestionSeeder(
        CatalogDbContext db,
        IClock clock,
        IHostEnvironment environment,
        ILogger<DevelopmentBankQuestionSeeder> logger)
    {
        _db = db;
        _clock = clock;
        _environment = environment;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment() && !_environment.IsEnvironment("Testing"))
        {
            _logger.LogWarning("Skipped Catalog bank development seed in {Environment}", _environment.EnvironmentName);
            return;
        }

        await AddIfMissingAsync(
            SampleBankMcSingle,
            "HTTP 201 Created",
            3,
            QuestionType.MultipleChoiceSingle,
            "Which HTTP status means a resource was created?",
            ScoringMode.Auto,
            creditMode: null,
            points: 1,
            new
            {
                options = new[]
                {
                    new { id = "opt-200", text = "200 OK", isCorrect = false },
                    new { id = "opt-201", text = "201 Created", isCorrect = true },
                    new { id = "opt-204", text = "204 No Content", isCorrect = false }
                }
            },
            cancellationToken);

        await AddIfMissingAsync(
            SampleBankTrueFalse,
            "One opening per quiz",
            0,
            QuestionType.TrueFalse,
            "A quiz belongs to exactly one opening.",
            ScoringMode.Auto,
            creditMode: null,
            points: 1,
            new { correct = true },
            cancellationToken);

        await AddIfMissingAsync(
            SampleBankShortText,
            "REST acronym",
            5,
            QuestionType.ShortText,
            "What does REST stand for?",
            ScoringMode.Auto,
            creditMode: null,
            points: 1,
            new
            {
                acceptableAnswers = new[] { "Representational State Transfer" },
                caseSensitive = false
            },
            cancellationToken);
    }

    private async Task AddIfMissingAsync(
        Guid id,
        string title,
        int expectedExperienceYears,
        QuestionType type,
        string stem,
        ScoringMode scoringMode,
        CreditMode? creditMode,
        int points,
        object body,
        CancellationToken cancellationToken)
    {
        if (await _db.BankQuestions.AnyAsync(q => q.Id == id, cancellationToken))
        {
            return;
        }

        _db.BankQuestions.Add(BankQuestion.Create(
            title,
            new Dictionary<string, string>
            {
                ["Client"] = "Internal",
                ["Topic"] = "HTTP"
            },
            expectedExperienceYears,
            type,
            stem,
            scoringMode,
            creditMode,
            points,
            Body(body),
            _clock,
            id));

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded bank question {QuestionId}", id);
    }

    private static JsonElement Body(object value)
        => JsonSerializer.SerializeToElement(value, CatalogJson.SerializerOptions);
}
