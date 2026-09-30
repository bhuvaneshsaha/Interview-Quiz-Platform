using System.Text.Json;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Catalog.Infrastructure.Persistence;
using InterviewQuiz.Kernel.Clock;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InterviewQuiz.Catalog.Infrastructure.Seeding;

public sealed class DevelopmentQuizSeeder
{
    /// <summary>Must match <c>DevelopmentOpeningSeeder.SampleOpeningBackend</c>.</summary>
    public static readonly Guid SampleOpeningBackend = Guid.Parse("3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001");
    public static readonly Guid SampleQuizBackend = Guid.Parse("4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2001");
    public static readonly Guid SampleTemplateBackend = Guid.Parse("5c9e3f32-8d4f-4e1c-9a33-2b0e4d8c3001");
    public static readonly Guid SampleTemplateVersion1 = Guid.Parse("5c9e3f32-8d4f-4e1c-9a33-2b0e4d8c3101");

    private readonly CatalogDbContext _db;
    private readonly IClock _clock;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DevelopmentQuizSeeder> _logger;

    public DevelopmentQuizSeeder(
        CatalogDbContext db,
        IClock clock,
        IHostEnvironment environment,
        ILogger<DevelopmentQuizSeeder> logger)
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
            _logger.LogWarning("Skipped Catalog development seed in {Environment}", _environment.EnvironmentName);
            return;
        }

        if (!await _db.Quizzes.AnyAsync(q => q.Id == SampleQuizBackend, cancellationToken))
        {
            await SeedQuizAsync(cancellationToken);
        }

        await SeedTemplateAsync(cancellationToken);
    }

    private async Task SeedQuizAsync(CancellationToken cancellationToken)
    {
        var questions = new[]
        {
            Question.Create(
                Guid.Parse("4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2101"),
                0,
                QuestionType.MultipleChoiceSingle,
                "Which HTTP status means a resource was created?",
                ScoringMode.Auto,
                creditMode: null,
                points: 1,
                Body(new
                {
                    options = new[]
                    {
                        new { id = "opt-200", text = "200 OK", isCorrect = false },
                        new { id = "opt-201", text = "201 Created", isCorrect = true },
                        new { id = "opt-204", text = "204 No Content", isCorrect = false }
                    }
                })),
            Question.Create(
                Guid.Parse("4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2102"),
                1,
                QuestionType.MultipleChoiceMulti,
                "Which of these are ASP.NET Core hosting concerns?",
                ScoringMode.Auto,
                CreditMode.Partial,
                points: 2,
                Body(new
                {
                    options = new[]
                    {
                        new { id = "opt-kestrel", text = "Kestrel", isCorrect = true },
                        new { id = "opt-mediator", text = "A required mediator library", isCorrect = false },
                        new { id = "opt-health", text = "Health checks", isCorrect = true }
                    }
                })),
            Question.Create(
                Guid.Parse("4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2103"),
                2,
                QuestionType.TrueFalse,
                "A quiz belongs to exactly one opening.",
                ScoringMode.Auto,
                creditMode: null,
                points: 1,
                Body(new { correct = true })),
            Question.Create(
                Guid.Parse("4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2104"),
                3,
                QuestionType.ShortText,
                "What does REST stand for?",
                ScoringMode.Auto,
                creditMode: null,
                points: 1,
                Body(new
                {
                    acceptableAnswers = new[] { "Representational State Transfer" },
                    caseSensitive = false
                })),
            Question.Create(
                Guid.Parse("4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2105"),
                4,
                QuestionType.LongText,
                "Describe how you would keep a Modular Monolith's module boundaries honest.",
                ScoringMode.HumanOnly,
                creditMode: null,
                points: 5,
                Body(new
                {
                    maxLength = 4000,
                    guidance = "Mention public contracts, schemas, and no cross-module table writes."
                })),
            Question.Create(
                Guid.Parse("4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2106"),
                5,
                QuestionType.DragDropSharedBank,
                "Match each HTTP method to what it typically does.",
                ScoringMode.Auto,
                creditMode: null,
                points: 3,
                Body(new
                {
                    slots = new[]
                    {
                        new { id = "slot-get", label = "GET", correctItemId = "item-read" },
                        new { id = "slot-post", label = "POST", correctItemId = "item-create" }
                    },
                    bank = new[]
                    {
                        new { id = "item-read", text = "Read a resource", isDistractor = false },
                        new { id = "item-create", text = "Create a resource", isDistractor = false },
                        new { id = "item-format", text = "Change the response encoding only", isDistractor = true }
                    }
                })),
            Question.Create(
                Guid.Parse("4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2107"),
                6,
                QuestionType.DragDropPerSlot,
                "For each status code, pick the matching meaning.",
                ScoringMode.Auto,
                creditMode: null,
                points: 2,
                Body(new
                {
                    slots = new[]
                    {
                        new
                        {
                            id = "slot-401",
                            label = "401",
                            options = new[]
                            {
                                new { id = "opt-unauth", text = "Unauthenticated", isCorrect = true },
                                new { id = "opt-forbid", text = "Forbidden", isCorrect = false }
                            }
                        },
                        new
                        {
                            id = "slot-403",
                            label = "403",
                            options = new[]
                            {
                                new { id = "opt-unauth-2", text = "Unauthenticated", isCorrect = false },
                                new { id = "opt-forbid-2", text = "Authenticated but not permitted", isCorrect = true }
                            }
                        }
                    }
                })),
            Question.Create(
                Guid.Parse("4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2108"),
                7,
                QuestionType.Ordering,
                "Order the request pipeline from first to last.",
                ScoringMode.Auto,
                CreditMode.Partial,
                points: 3,
                Body(new
                {
                    items = new[]
                    {
                        new { id = "item-authn", text = "Authentication", correctIndex = 0 },
                        new { id = "item-authz", text = "Authorization", correctIndex = 1 },
                        new { id = "item-endpoint", text = "Endpoint", correctIndex = 2 }
                    }
                }))
        };

        _db.Quizzes.Add(Quiz.Create(
            openingId: SampleOpeningBackend,
            title: "Backend interview — working copy",
            description: "Sample authoring quiz under the Senior backend engineer opening.",
            expectedExperienceYears: 5,
            tags: new Dictionary<string, string>
            {
                ["Client"] = "Internal",
                ["Project"] = "InterviewQuiz",
                ["Role"] = "Backend"
            },
            questions: questions,
            clock: _clock,
            id: SampleQuizBackend));

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded sample quiz {QuizId}", SampleQuizBackend);
    }

    private async Task SeedTemplateAsync(CancellationToken cancellationToken)
    {
        if (await _db.Templates.AnyAsync(t => t.Id == SampleTemplateBackend, cancellationToken))
        {
            return;
        }

        var quiz = await _db.Quizzes.FirstAsync(q => q.Id == SampleQuizBackend, cancellationToken);
        var template = Template.Create(quiz.Id, _clock, SampleTemplateBackend);
        var copies = quiz.Questions
            .OrderBy(q => q.SortOrder)
            .ThenBy(q => q.Id)
            .Select((question, index) => question.CopyWithNewId(index, SeedTemplateQuestionId(index)))
            .ToList();

        var version = TemplateVersion.FromQuiz(
            template.Id,
            versionNumber: 1,
            quiz,
            publishedByUserId: "development-seeder",
            _clock,
            SampleTemplateVersion1,
            copies);

        quiz.AttachToTemplate(template.Id, _clock);
        _db.Templates.Add(template);
        _db.TemplateVersions.Add(version);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Seeded sample template {TemplateId} version {VersionId}",
            SampleTemplateBackend,
            SampleTemplateVersion1);
    }

    private static Guid SeedTemplateQuestionId(int index)
        => Guid.Parse($"5c9e3f32-8d4f-4e1c-9a33-2b0e4d8c32{index + 1:00}");

    private static JsonElement Body(object value)
        => JsonSerializer.SerializeToElement(value, CatalogJson.SerializerOptions);
}
