namespace InterviewQuiz.Openings.Application.Contracts;

public sealed class OpeningResponse
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required string JobDescription { get; init; }
    public required string Owner { get; init; }
    public required DateOnly StartDate { get; init; }
    public DateOnly? ExpectedCloseDate { get; init; }
    public required int Headcount { get; init; }
    public required int ExpectedExperienceYears { get; init; }
    public required IReadOnlyList<string> Handlers { get; init; }
    public required IReadOnlyDictionary<string, string> Tags { get; init; }
    public required uint RowVersion { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; init; }
}
