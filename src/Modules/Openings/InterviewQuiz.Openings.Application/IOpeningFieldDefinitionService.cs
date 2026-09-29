using InterviewQuiz.Openings.Application.Contracts;

namespace InterviewQuiz.Openings.Application;

public interface IOpeningFieldDefinitionService
{
    Task<IReadOnlyList<OpeningFieldDefinitionResponse>> ListAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<OpeningFieldDefinitionResponse>> ReplaceAsync(ReplaceOpeningFieldDefinitionsRequest request, CancellationToken cancellationToken);
}
