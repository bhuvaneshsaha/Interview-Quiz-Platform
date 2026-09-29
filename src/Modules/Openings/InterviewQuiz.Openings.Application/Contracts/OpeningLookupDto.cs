namespace InterviewQuiz.Openings.Application.Contracts;

/// <summary>
/// In-process opening summary for Catalog and Delivery. Other modules must not query openings tables.
/// </summary>
public sealed record OpeningLookupDto(
    Guid Id,
    string Title,
    string Owner,
    DateOnly StartDate,
    DateOnly? ExpectedCloseDate,
    int Headcount,
    int ExpectedExperienceYears);
