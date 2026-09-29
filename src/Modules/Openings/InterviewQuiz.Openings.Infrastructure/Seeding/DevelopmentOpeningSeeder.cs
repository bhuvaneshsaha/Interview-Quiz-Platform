using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Openings.Domain;
using InterviewQuiz.Openings.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InterviewQuiz.Openings.Infrastructure.Seeding;

public sealed class DevelopmentOpeningSeeder
{
    public static readonly Guid SampleOpeningBackend = Guid.Parse("3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001");
    public static readonly Guid SampleOpeningQa = Guid.Parse("3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1002");

    private readonly OpeningsDbContext _db;
    private readonly IClock _clock;

    public DevelopmentOpeningSeeder(OpeningsDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!await _db.OpeningFieldDefinitions.AnyAsync(cancellationToken))
        {
            _db.OpeningFieldDefinitions.AddRange(
                OpeningFieldDefinition.Create("Client", "Client", 0),
                OpeningFieldDefinition.Create("Project", "Project", 1),
                OpeningFieldDefinition.Create("Role", "Role", 2));
        }

        if (!await _db.Openings.AnyAsync(o => o.Id == SampleOpeningBackend, cancellationToken))
        {
            _db.Openings.Add(Opening.Create(
                title: "Senior backend engineer",
                jobDescription: "Design and ship the interview quiz API with ASP.NET Core.",
                owner: "recruiter.dev@example.com",
                startDate: new DateOnly(2026, 10, 1),
                expectedCloseDate: new DateOnly(2026, 12, 15),
                headcount: 2,
                expectedExperienceYears: 5,
                handlers: ["recruiter.dev@example.com", "hiring.lead@example.com"],
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
            _db.Openings.Add(Opening.Create(
                title: "QA specialist (templates)",
                jobDescription: "Author QA interview templates and review written answers.",
                owner: "qa.lead@example.com",
                startDate: new DateOnly(2026, 9, 15),
                expectedCloseDate: new DateOnly(2026, 11, 30),
                headcount: 1,
                expectedExperienceYears: 3,
                handlers: ["qa.lead@example.com"],
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
}
