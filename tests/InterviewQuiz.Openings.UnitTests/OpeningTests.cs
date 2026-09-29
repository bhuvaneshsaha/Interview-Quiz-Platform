using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Openings.Domain;

namespace InterviewQuiz.Openings.UnitTests;

public sealed class OpeningTests
{
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Create_sets_core_fields_and_normalizes_tags()
    {
        var opening = Opening.Create(
            title: "  Backend  ",
            jobDescription: "Build APIs",
            owner: "owner@example.com",
            startDate: new DateOnly(2026, 10, 1),
            expectedCloseDate: new DateOnly(2026, 11, 1),
            headcount: 2,
            expectedExperienceYears: 5,
            handlers: ["a@example.com", "A@example.com", "b@example.com"],
            tags: new Dictionary<string, string> { ["Client"] = " Acme " },
            clock: _clock);

        Assert.Equal("Backend", opening.Title);
        Assert.Equal(2, opening.Handlers.Count);
        Assert.Equal("Acme", opening.Tags["Client"]);
        Assert.Equal(_clock.UtcNow, opening.CreatedAtUtc);
    }

    [Fact]
    public void Create_rejects_empty_title()
    {
        var ex = Assert.Throws<DomainException>(() => CreateValid(title: " "));
        Assert.Contains("Title", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_rejects_headcount_below_one()
    {
        var ex = Assert.Throws<DomainException>(() => CreateValid(headcount: 0));
        Assert.Contains("Headcount", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_rejects_negative_experience()
    {
        var ex = Assert.Throws<DomainException>(() => CreateValid(experience: -1));
        Assert.Contains("experience", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_rejects_close_date_before_start()
    {
        var ex = Assert.Throws<DomainException>(() => CreateValid(
            start: new DateOnly(2026, 10, 1),
            close: new DateOnly(2026, 9, 1)));
        Assert.Contains("close date", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Update_changes_fields_and_timestamp()
    {
        var opening = CreateValid();
        var later = new FixedClock(_clock.UtcNow.AddHours(2));

        opening.Update(
            title: "Updated",
            jobDescription: opening.JobDescription,
            owner: opening.Owner,
            startDate: opening.StartDate,
            expectedCloseDate: opening.ExpectedCloseDate,
            headcount: 3,
            expectedExperienceYears: 4,
            handlers: opening.Handlers,
            tags: new Dictionary<string, string> { ["Project"] = "Phoenix" },
            clock: later);

        Assert.Equal("Updated", opening.Title);
        Assert.Equal(3, opening.Headcount);
        Assert.Equal("Phoenix", opening.Tags["Project"]);
        Assert.Equal(later.UtcNow, opening.UpdatedAtUtc);
        Assert.Equal(1u, opening.RowVersion);
    }

    [Fact]
    public void Create_rejects_invalid_tag_key()
    {
        Assert.Throws<DomainException>(() => CreateValid(tags: new Dictionary<string, string>
        {
            ["Client Name"] = "Acme"
        }));
    }

    private Opening CreateValid(
        string title = "Opening",
        int headcount = 1,
        int experience = 3,
        DateOnly? start = null,
        DateOnly? close = null,
        Dictionary<string, string>? tags = null)
        => Opening.Create(
            title,
            "JD",
            "owner@example.com",
            start ?? new DateOnly(2026, 10, 1),
            close ?? new DateOnly(2026, 12, 1),
            headcount,
            experience,
            ["handler@example.com"],
            tags ?? new Dictionary<string, string> { ["Client"] = "Acme" },
            _clock);

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow) => UtcNow = utcNow;
        public DateTimeOffset UtcNow { get; }
    }
}
