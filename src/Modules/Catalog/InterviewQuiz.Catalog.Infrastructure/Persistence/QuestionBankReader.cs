using InterviewQuiz.Catalog.Application;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;

namespace InterviewQuiz.Catalog.Infrastructure.Persistence;

public sealed class QuestionBankReader : IQuestionBankReader
{
    private readonly IBankQuestionRepository _bank;

    public QuestionBankReader(IBankQuestionRepository bank)
    {
        _bank = bank;
    }

    public async Task<QuestionBankItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await _bank.GetAsync(id, cancellationToken);
        return item is null ? null : Map(item);
    }

    private static QuestionBankItemDto Map(BankQuestion item)
        => new()
        {
            Id = item.Id,
            SortOrder = 0,
            Type = item.Type,
            Stem = item.Stem,
            ScoringMode = item.ScoringMode,
            CreditMode = item.CreditMode,
            Points = item.Points,
            Body = item.Body.RootElement.Clone(),
            SourceQuestionId = null,
            Title = item.Title,
            Tags = item.Tags,
            ExpectedExperienceYears = item.ExpectedExperienceYears,
            ArchivedAtUtc = item.ArchivedAtUtc
        };
}
