using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Openings.Application;
using InterviewQuiz.Openings.Application.Contracts;
using InterviewQuiz.Openings.Application.Services;
using InterviewQuiz.Openings.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace InterviewQuiz.Openings.UnitTests;

public sealed class OpeningFieldDefinitionServiceTests
{
    [Fact]
    public async Task ReplaceAsync_rejects_duplicate_keys()
    {
        var service = new OpeningFieldDefinitionService(
            new FakeFieldDefinitionRepository(),
            NullLogger<OpeningFieldDefinitionService>.Instance);

        var request = new ReplaceOpeningFieldDefinitionsRequest
        {
            Items =
            [
                new OpeningFieldDefinitionDto { Key = "Client", DisplayName = "Client", SortOrder = 0 },
                new OpeningFieldDefinitionDto { Key = "client", DisplayName = "Client 2", SortOrder = 1 }
            ]
        };

        await Assert.ThrowsAsync<DomainException>(() =>
            service.ReplaceAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task ReplaceAsync_replaces_the_catalog()
    {
        var repo = new FakeFieldDefinitionRepository();
        var service = new OpeningFieldDefinitionService(repo, NullLogger<OpeningFieldDefinitionService>.Instance);

        var result = await service.ReplaceAsync(new ReplaceOpeningFieldDefinitionsRequest
        {
            Items =
            [
                new OpeningFieldDefinitionDto { Key = "Client", DisplayName = "Client", SortOrder = 0 },
                new OpeningFieldDefinitionDto { Key = "Project", DisplayName = "Project", SortOrder = 1 }
            ]
        }, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(2, repo.Items.Count);
    }

    private sealed class FakeFieldDefinitionRepository : IOpeningFieldDefinitionRepository
    {
        public List<OpeningFieldDefinition> Items { get; } = [];

        public Task<IReadOnlyList<OpeningFieldDefinition>> ListAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<OpeningFieldDefinition>>(Items);

        public Task ReplaceAllAsync(IReadOnlyList<OpeningFieldDefinition> definitions, CancellationToken cancellationToken)
        {
            Items.Clear();
            Items.AddRange(definitions);
            return Task.CompletedTask;
        }
    }
}
