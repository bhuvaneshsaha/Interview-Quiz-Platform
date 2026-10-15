namespace InterviewQuiz.Openings.Application.Contracts;

public sealed class OpeningPersonResponse
{
    public required string UserId { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required bool Resolved { get; init; }
}

public sealed class OpeningResponse
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required string JobDescription { get; init; }

    /// <summary>User id, or the original text when a migration could not match a user.</summary>
    public required string Owner { get; init; }
    public required string OwnerName { get; init; }
    public required string OwnerEmail { get; init; }
    public required bool OwnerResolved { get; init; }
    public required DateOnly StartDate { get; init; }
    public DateOnly? ExpectedCloseDate { get; init; }
    public required int Headcount { get; init; }
    public required int ExpectedExperienceYears { get; init; }

    /// <summary>User ids, plus any unmatched legacy values kept by migration.</summary>
    public required IReadOnlyList<string> Handlers { get; init; }
    public required IReadOnlyList<OpeningPersonResponse> HandlerPeople { get; init; }
    public required IReadOnlyDictionary<string, string> Tags { get; init; }
    public required uint RowVersion { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; init; }
}
