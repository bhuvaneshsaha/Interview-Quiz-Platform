using System.Text.Json;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Delivery.Application;
using InterviewQuiz.Delivery.Application.Contracts;
using InterviewQuiz.Evaluation.Application;
using InterviewQuiz.Evaluation.Application.Services;
using InterviewQuiz.Evaluation.Domain;
using InterviewQuiz.Kernel.Assignments;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Pagination;
using Microsoft.Extensions.Logging.Abstractions;

namespace InterviewQuiz.Evaluation.UnitTests;

public sealed class AttemptServiceStartTests
{
    private static readonly Guid AssignmentId = Guid.Parse("7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001");
    private static readonly Guid OpeningId = Guid.Parse("3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001");
    private static readonly Guid QuizId = Guid.Parse("4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2001");
    private static readonly Guid QuestionId = Guid.Parse("4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2101");

    [Fact]
    public async Task Start_after_due_returns_the_submitted_attempt()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));
        var attempts = new InMemoryAttempts();
        var lifecycle = new RecordingLifecycle();
        var header = Header();
        var started = Attempt.Start(
            AssignmentId,
            OpeningId,
            header.SnapshotId,
            "candidate.dev@example.com",
            30,
            JsonDocument.Parse("{}"),
            clock);
        attempts.Seed(started);

        clock.UtcNow = started.DueAtUtc.AddMinutes(1);
        var service = new AttemptService(
            attempts,
            new FixedSnapshotReader(header),
            lifecycle,
            clock,
            NullLogger<AttemptService>.Instance);

        var (response, created) = await service.StartAsync(AssignmentId, CancellationToken.None);

        Assert.False(created);
        Assert.Equal("submitted", response.Status);
        Assert.Equal(started.Id, response.Id);
        Assert.Equal(0, response.RemainingSeconds);
        Assert.NotNull(response.ItemResults);
        Assert.Single(lifecycle.Submitted);
        Assert.Empty(lifecycle.Started);
    }

    [Fact]
    public async Task Start_while_in_progress_does_not_submit()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));
        var attempts = new InMemoryAttempts();
        var lifecycle = new RecordingLifecycle();
        var header = Header();
        var started = Attempt.Start(
            AssignmentId,
            OpeningId,
            header.SnapshotId,
            "candidate.dev@example.com",
            30,
            JsonDocument.Parse("{}"),
            clock);
        attempts.Seed(started);

        var service = new AttemptService(
            attempts,
            new FixedSnapshotReader(header),
            lifecycle,
            clock,
            NullLogger<AttemptService>.Instance);

        var (response, created) = await service.StartAsync(AssignmentId, CancellationToken.None);

        Assert.False(created);
        Assert.Equal("inProgress", response.Status);
        Assert.Empty(lifecycle.Submitted);
    }

    private static AssignmentWithSnapshotDto Header()
    {
        var snapshot = new QuizSnapshotDto(
            QuizId,
            OpeningId,
            "Seed quiz",
            "",
            1,
            new Dictionary<string, string>(),
            [
                new QuizSnapshotQuestionDto(
                    QuestionId,
                    0,
                    QuestionType.TrueFalse,
                    "REST is stateless.",
                    ScoringMode.Auto,
                    null,
                    1,
                    JsonSerializer.SerializeToElement(new { correct = true }, CatalogJson.SerializerOptions),
                    null)
            ],
            1,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);

        return new AssignmentWithSnapshotDto(
            AssignmentId,
            OpeningId,
            QuizId,
            Guid.Parse("7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5101"),
            "candidate.dev@example.com",
            "async",
            30,
            1,
            "inProgress",
            snapshot);
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow) => UtcNow = utcNow;

        public DateTimeOffset UtcNow { get; set; }
    }

    private sealed class FixedSnapshotReader(AssignmentWithSnapshotDto header) : IAssignmentSnapshotReader
    {
        public Task<AssignmentWithSnapshotDto?> GetAssignmentWithSnapshotAsync(
            Guid assignmentId,
            CancellationToken cancellationToken)
            => Task.FromResult<AssignmentWithSnapshotDto?>(
                assignmentId == header.Id ? header : null);
    }

    private sealed class RecordingLifecycle : IAssignmentLifecycle
    {
        public List<Guid> Started { get; } = [];
        public List<Guid> Submitted { get; } = [];

        public Task NotifyAttemptStarted(Guid assignmentId, Guid attemptId, CancellationToken cancellationToken)
        {
            Started.Add(attemptId);
            return Task.CompletedTask;
        }

        public Task NotifyAttemptSubmitted(
            Guid assignmentId,
            Guid attemptId,
            string resultStatus,
            CancellationToken cancellationToken)
        {
            Submitted.Add(attemptId);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryAttempts : IAttemptRepository
    {
        private readonly List<Attempt> _items = [];

        public void Seed(Attempt attempt) => _items.Add(attempt);

        public Task<Attempt?> GetAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(_items.FirstOrDefault(a => a.Id == id));

        public Task<Attempt?> GetInProgressAsync(Guid assignmentId, CancellationToken cancellationToken)
            => Task.FromResult(_items.FirstOrDefault(a => a.AssignmentId == assignmentId && !a.IsSubmitted));

        public Task<Attempt?> GetLatestAsync(Guid assignmentId, CancellationToken cancellationToken)
            => Task.FromResult(_items.LastOrDefault(a => a.AssignmentId == assignmentId));

        public Task<int> CountSubmittedAsync(Guid assignmentId, CancellationToken cancellationToken)
            => Task.FromResult(_items.Count(a => a.AssignmentId == assignmentId && a.IsSubmitted));

        public Task AddAsync(Attempt attempt, CancellationToken cancellationToken)
        {
            _items.Add(attempt);
            return Task.CompletedTask;
        }

        public Task<PagedResult<Attempt>> ListByAssignmentAsync(
            Guid assignmentId,
            PageRequest page,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
