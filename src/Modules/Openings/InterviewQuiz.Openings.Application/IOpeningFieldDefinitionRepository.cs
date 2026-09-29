using InterviewQuiz.Openings.Domain;

namespace InterviewQuiz.Openings.Application;

public interface IOpeningFieldDefinitionRepository
{
    Task<IReadOnlyList<OpeningFieldDefinition>> ListAsync(CancellationToken cancellationToken);
    Task ReplaceAllAsync(IReadOnlyList<OpeningFieldDefinition> definitions, CancellationToken cancellationToken);
}
