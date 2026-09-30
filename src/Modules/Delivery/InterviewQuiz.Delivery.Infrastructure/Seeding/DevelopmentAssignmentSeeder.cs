using InterviewQuiz.Catalog.Application;
using InterviewQuiz.Delivery.Domain;
using InterviewQuiz.Delivery.Infrastructure.Persistence;
using InterviewQuiz.Kernel.Clock;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InterviewQuiz.Delivery.Infrastructure.Seeding;

public sealed class DevelopmentAssignmentSeeder
{
    public static readonly Guid SampleAssignment = Guid.Parse("7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001");
    public static readonly Guid SampleOpeningBackend = Guid.Parse("3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001");
    public static readonly Guid SampleQuizBackend = Guid.Parse("4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2001");
    public const string SampleCandidateEmail = "candidate.dev@example.com";

    private readonly DeliveryDbContext _db;
    private readonly IQuizSnapshotReader _quizzes;
    private readonly IClock _clock;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DevelopmentAssignmentSeeder> _logger;

    public DevelopmentAssignmentSeeder(
        DeliveryDbContext db,
        IQuizSnapshotReader quizzes,
        IClock clock,
        IHostEnvironment environment,
        ILogger<DevelopmentAssignmentSeeder> logger)
    {
        _db = db;
        _quizzes = quizzes;
        _clock = clock;
        _environment = environment;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment() && !_environment.IsEnvironment("Testing"))
        {
            _logger.LogWarning("Skipped Delivery development seed in {Environment}", _environment.EnvironmentName);
            return;
        }

        if (await _db.Assignments.AnyAsync(a => a.Id == SampleAssignment, cancellationToken))
        {
            return;
        }

        var snapshotDto = await _quizzes.GetSnapshotAsync(SampleQuizBackend, cancellationToken);
        if (snapshotDto is null)
        {
            _logger.LogWarning("Skipped assignment seed; quiz {QuizId} is missing", SampleQuizBackend);
            return;
        }

        var payload = Application.SnapshotJson.FromDto(snapshotDto);
        var snapshot = AssignmentSnapshot.Freeze(
            Guid.NewGuid(),
            SampleAssignment,
            snapshotDto.Title,
            snapshotDto.Questions.Count,
            payload,
            _clock.UtcNow);

        var assignment = Assignment.Create(
            SampleOpeningBackend,
            SampleQuizBackend,
            SampleCandidateEmail,
            AssignmentMode.Async,
            overallDurationMinutes: 30,
            attemptLimit: 1,
            createdByUserId: "development-seeder",
            snapshot,
            _clock,
            SampleAssignment);

        _db.Assignments.Add(assignment);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Seeded sample assignment {AssignmentId} snapshot {SnapshotId}",
            assignment.Id,
            assignment.SnapshotId);
    }
}
