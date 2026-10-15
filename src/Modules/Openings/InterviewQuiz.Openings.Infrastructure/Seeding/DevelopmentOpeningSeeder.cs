using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Users;
using InterviewQuiz.Openings.Domain;
using InterviewQuiz.Openings.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InterviewQuiz.Openings.Infrastructure.Seeding;

public sealed class DevelopmentOpeningSeeder
{
    public static readonly Guid SampleOpeningBackend = Guid.Parse("3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001");
    public static readonly Guid SampleOpeningQa = Guid.Parse("3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1002");

    private readonly OpeningsDbContext _db;
    private readonly IAssignableUserLookup _users;
    private readonly IClock _clock;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DevelopmentOpeningSeeder> _logger;

    public DevelopmentOpeningSeeder(
        OpeningsDbContext db,
        IAssignableUserLookup users,
        IClock clock,
        IHostEnvironment environment,
        ILogger<DevelopmentOpeningSeeder> logger)
    {
        _db = db;
        _users = users;
        _clock = clock;
        _environment = environment;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment() && !_environment.IsEnvironment("Testing"))
        {
            _logger.LogWarning("Skipped Openings development seed in {Environment}", _environment.EnvironmentName);
            return;
        }

        if (!await _db.OpeningFieldDefinitions.AnyAsync(cancellationToken))
        {
            _db.OpeningFieldDefinitions.AddRange(
                OpeningFieldDefinition.Create("Client", "Client", 0),
                OpeningFieldDefinition.Create("Project", "Project", 1),
                OpeningFieldDefinition.Create("Role", "Role", 2));
        }

        if (!await _db.Openings.AnyAsync(o => o.Id == SampleOpeningBackend, cancellationToken))
        {
            var recruiterId = await RequireUserIdAsync("recruiter.dev@example.com", cancellationToken);
            var authorId = await RequireUserIdAsync("author.dev@example.com", cancellationToken);
            _db.Openings.Add(Opening.Create(
                title: "Senior backend engineer",
                jobDescription: "Design and ship the interview quiz API with ASP.NET Core.",
                owner: recruiterId,
                startDate: new DateOnly(2026, 10, 1),
                expectedCloseDate: new DateOnly(2026, 12, 15),
                headcount: 2,
                expectedExperienceYears: 5,
                handlers: [recruiterId, authorId],
                tags: new Dictionary<string, string>
                {
                    ["Client"] = "Internal",
                    ["Project"] = "InterviewQuiz",
                    ["Role"] = "Backend"
                },
                clock: _clock,
                id: SampleOpeningBackend));
        }

        if (!await _db.Openings.AnyAsync(o => o.Id == SampleOpeningQa, cancellationToken))
        {
            var reviewerId = await RequireUserIdAsync("reviewer.dev@example.com", cancellationToken);
            var authorId = await RequireUserIdAsync("author.dev@example.com", cancellationToken);
            _db.Openings.Add(Opening.Create(
                title: "QA specialist (templates)",
                jobDescription: "Author QA interview templates and review written answers.",
                owner: reviewerId,
                startDate: new DateOnly(2026, 9, 15),
                expectedCloseDate: new DateOnly(2026, 11, 30),
                headcount: 1,
                expectedExperienceYears: 3,
                handlers: [reviewerId, authorId],
                tags: new Dictionary<string, string>
                {
                    ["Client"] = "Internal",
                    ["Project"] = "InterviewQuiz",
                    ["Role"] = "QA"
                },
                clock: _clock,
                id: SampleOpeningQa));
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> RequireUserIdAsync(string email, CancellationToken cancellationToken)
    {
        var matches = await _users.SearchAsync(email, 20, cancellationToken);
        var exact = matches.FirstOrDefault(user =>
            user.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        if (exact is null)
        {
            throw new InvalidOperationException($"Development user {email} was not seeded.");
        }

        return exact.Id;
    }
}
