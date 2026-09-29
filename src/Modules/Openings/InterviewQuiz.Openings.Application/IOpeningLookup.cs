using InterviewQuiz.Openings.Application.Contracts;

namespace InterviewQuiz.Openings.Application;

/// <summary>
/// In-process lookup for Catalog and Delivery. Do not query the openings schema from other modules.
/// </summary>
public interface IOpeningLookup
{
    Task<OpeningLookupDto?> GetOpeningAsync(Guid id, CancellationToken cancellationToken);
}
