using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Openings.Application.Contracts;
using InterviewQuiz.Openings.Domain;
using Microsoft.Extensions.Logging;

namespace InterviewQuiz.Openings.Application.Services;

public sealed class OpeningFieldDefinitionService : IOpeningFieldDefinitionService
{
    private readonly IOpeningFieldDefinitionRepository _definitions;
    private readonly ILogger<OpeningFieldDefinitionService> _logger;

    public OpeningFieldDefinitionService(
        IOpeningFieldDefinitionRepository definitions,
        ILogger<OpeningFieldDefinitionService> logger)
    {
        _definitions = definitions;
        _logger = logger;
    }

    public async Task<IReadOnlyList<OpeningFieldDefinitionResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var items = await _definitions.ListAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<OpeningFieldDefinitionResponse>> ReplaceAsync(
        ReplaceOpeningFieldDefinitionsRequest request,
        CancellationToken cancellationToken)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var definitions = new List<OpeningFieldDefinition>();
        foreach (var item in request.Items)
        {
            var definition = OpeningFieldDefinition.Create(item.Key, item.DisplayName, item.SortOrder);
            if (!keys.Add(definition.Key))
            {
                throw new DomainException($"Duplicate field key '{definition.Key}'.");
            }

            definitions.Add(definition);
        }

        await _definitions.ReplaceAllAsync(definitions, cancellationToken);
        _logger.LogInformation("Replaced opening field definitions. Count {Count}", definitions.Count);
        return definitions.Select(Map).ToList();
    }

    private static OpeningFieldDefinitionResponse Map(OpeningFieldDefinition definition) => new()
    {
        Id = definition.Id,
        Key = definition.Key,
        DisplayName = definition.DisplayName,
        SortOrder = definition.SortOrder
    };
}
