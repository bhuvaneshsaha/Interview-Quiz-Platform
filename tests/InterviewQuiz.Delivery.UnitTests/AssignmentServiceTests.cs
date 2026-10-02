using System.Text.Json;
using InterviewQuiz.Catalog.Application;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Delivery.Application;
using InterviewQuiz.Delivery.Application.Contracts;
using InterviewQuiz.Delivery.Application.Services;
using InterviewQuiz.Delivery.Domain;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Openings.Application;
using InterviewQuiz.Openings.Application.Contracts;
using Microsoft.Extensions.Logging.Abstractions;

namespace InterviewQuiz.Delivery.UnitTests;

public sealed class AssignmentServiceTests
{
    private static readonly Guid OpeningId = Guid.Parse("3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001");
    private static readonly Guid QuizId = Guid.Parse("4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2001");
    private static readonly Guid QuestionId = Guid.Parse("4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2101");
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task CreateAsync_rejects_unknown_opening()
    {
        var service = CreateService(openingExists: false, snapshot: Snapshot());
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateAsync(ValidCreate(), "user-1", CancellationToken.None));
        Assert.Equal(Assignment.OpeningMissingMessage, ex.Message);
    }

    [Fact]
    public async Task CreateAsync_rejects_unknown_quiz()
    {
        var service = CreateService(openingExists: true, snapshot: null);
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateAsync(ValidCreate(), "user-1", CancellationToken.None));
        Assert.Equal(Assignment.QuizMissingMessage, ex.Message);
    }

    [Fact]
    public async Task CreateAsync_rejects_quiz_opening_mismatch()
    {
        var snapshot = Snapshot(openingId: Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        var service = CreateService(openingExists: true, snapshot);
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateAsync(ValidCreate(), "user-1", CancellationToken.None));
        Assert.Equal(Assignment.QuizOpeningMismatchMessage, ex.Message);
    }

    [Fact]
    public async Task CreateAsync_rejects_quiz_with_no_questions()
    {
        var snapshot = Snapshot(questions: []);
        var service = CreateService(openingExists: true, snapshot);
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateAsync(ValidCreate(), "user-1", CancellationToken.None));
        Assert.Equal(Assignment.QuizHasNoQuestionsMessage, ex.Message);
    }

    [Fact]
    public async Task CreateAsync_rejects_async_without_duration()
    {
        var service = CreateService(openingExists: true, snapshot: Snapshot());
        var request = ValidCreate();
        request.Timing = null;
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateAsync(request, "user-1", CancellationToken.None));
        Assert.Equal(Assignment.AsyncDurationRequiredMessage, ex.Message);
    }

    [Fact]
    public async Task CreateAsync_rejects_duration_out_of_range()
    {
        var service = CreateService(openingExists: true, snapshot: Snapshot());
        var request = ValidCreate();
        request.Timing = new AssignmentTimingRequest { OverallDurationMinutes = 500 };
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateAsync(request, "user-1", CancellationToken.None));
        Assert.Equal(Assignment.AsyncDurationRangeMessage, ex.Message);
    }

    [Fact]
    public async Task CreateAsync_copies_snapshot_preserving_question_ids()
    {
        var repo = new FakeAssignmentRepository();
        var service = CreateService(openingExists: true, snapshot: Snapshot(), repo);
        var created = await service.CreateAsync(ValidCreate(), "user-1", CancellationToken.None);

        Assert.NotEqual(QuizId, created.SnapshotId);
        Assert.Equal(1, created.SnapshotQuestionCount);
        Assert.Equal("async", created.Mode);
        Assert.Null(created.InviteUrl);

        var loaded = await service.GetAssignmentWithSnapshotAsync(created.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(QuizId, loaded!.QuizId);
        Assert.Equal(created.SnapshotId, loaded.SnapshotId);
        Assert.Single(loaded.Snapshot.Questions);
        Assert.Equal(QuestionId, loaded.Snapshot.Questions[0].Id);
        Assert.True(loaded.Snapshot.Questions[0].Body.TryGetProperty("options", out var options));
        Assert.True(options[1].GetProperty("isCorrect").GetBoolean());
    }

    [Fact]
    public async Task CreateAsync_live_omits_duration()
    {
        var service = CreateService(openingExists: true, snapshot: Snapshot());
        var request = ValidCreate();
        request.Mode = "live";
        request.Timing = null;
        var created = await service.CreateAsync(request, "user-1", CancellationToken.None);
        Assert.Equal("live", created.Mode);
        Assert.Null(created.OverallDurationMinutes);
        Assert.Null(created.InviteUrl);
    }

    private AssignmentService CreateService(
        bool openingExists,
        QuizSnapshotDto? snapshot,
        FakeAssignmentRepository? repo = null)
        => new(
            repo ?? new FakeAssignmentRepository(),
            new FakeOpeningLookup(openingExists ? OpeningId : null),
            new FakeQuizSnapshotReader(snapshot),
            _clock,
            NullLogger<AssignmentService>.Instance);

    private static CreateAssignmentRequest ValidCreate()
        => new()
        {
            OpeningId = OpeningId,
            QuizId = QuizId,
            CandidateEmail = "Candidate.Dev@example.com",
            Mode = "async",
            Timing = new AssignmentTimingRequest { OverallDurationMinutes = 30 },
            AttemptLimit = 1
        };

    private static QuizSnapshotDto Snapshot(Guid? openingId = null, IReadOnlyList<QuizSnapshotQuestionDto>? questions = null)
        => new(
            QuizId,
            openingId ?? OpeningId,
            "Backend interview — working copy",
            "Sample",
            5,
            new Dictionary<string, string>(),
            questions ??
            [
                new QuizSnapshotQuestionDto(
                    QuestionId,
                    0,
                    QuestionType.MultipleChoiceSingle,
                    "Which HTTP status means a resource was created?",
                    ScoringMode.Auto,
                    null,
                    1,
                    JsonSerializer.SerializeToElement(new
                    {
                        options = new[]
                        {
                            new { id = "opt-200", text = "200 OK", isCorrect = false },
                            new { id = "opt-201", text = "201 Created", isCorrect = true }
                        }
                    }, CatalogJson.SerializerOptions),
                    null)
            ],
            1,
            new DateTimeOffset(2026, 9, 29, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 29, 8, 0, 0, TimeSpan.Zero));

    private sealed class FakeQuizSnapshotReader : IQuizSnapshotReader
    {
        private readonly QuizSnapshotDto? _snapshot;

        public FakeQuizSnapshotReader(QuizSnapshotDto? snapshot) => _snapshot = snapshot;

        public Task<QuizSnapshotDto?> GetSnapshotAsync(Guid quizId, CancellationToken cancellationToken)
            => Task.FromResult(quizId == QuizId ? _snapshot : null);
    }

    private sealed class FakeOpeningLookup : IOpeningLookup
    {
        private readonly Guid? _existing;

        public FakeOpeningLookup(Guid? existing) => _existing = existing;

        public Task<OpeningLookupDto?> GetOpeningAsync(Guid id, CancellationToken cancellationToken)
        {
            if (_existing is null || _existing.Value != id)
            {
                return Task.FromResult<OpeningLookupDto?>(null);
            }

            return Task.FromResult<OpeningLookupDto?>(new OpeningLookupDto(
                id, "Opening", "owner@example.com", new DateOnly(2026, 10, 1), null, 1, 5));
        }
    }

    private sealed class FakeAssignmentRepository : IAssignmentRepository
    {
        public List<Assignment> Items { get; } = [];

        public Task<Assignment?> GetAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(Items.FirstOrDefault(a => a.Id == id));

        public Task AddAsync(Assignment assignment, CancellationToken cancellationToken)
        {
            Items.Add(assignment);
            return Task.CompletedTask;
        }

        public Task<PagedResult<Assignment>> ListAsync(
            Guid? openingId,
            string? keyword,
            PageRequest page,
            CancellationToken cancellationToken)
        {
            IEnumerable<Assignment> query = Items;
            if (openingId is { } opening)
            {
                query = query.Where(a => a.OpeningId == opening);
            }

            var list = query.OrderByDescending(a => a.CreatedAtUtc).ThenBy(a => a.Id).ToList();
            return Task.FromResult(new PagedResult<Assignment>(
                list.Skip(page.Skip).Take(page.PageSize).ToList(),
                page.Page,
                page.PageSize,
                list.Count));
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow) => UtcNow = utcNow;
        public DateTimeOffset UtcNow { get; }
    }
}
