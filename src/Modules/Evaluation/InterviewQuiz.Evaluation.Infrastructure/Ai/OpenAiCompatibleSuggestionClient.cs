using InterviewQuiz.Evaluation.Application.Ai;
using InterviewQuiz.Evaluation.Domain;
using InterviewQuiz.Kernel.Ai;
using InterviewQuiz.Kernel.Exceptions;

namespace InterviewQuiz.Evaluation.Infrastructure.Ai;

public sealed class OpenAiCompatibleSuggestionClient : IAiSuggestionClient
{
    private readonly IAiCompletionClient _completion;

    public OpenAiCompatibleSuggestionClient(IAiCompletionClient completion)
    {
        _completion = completion;
    }

    public bool IsEnabled => _completion.IsEnabled;

    public string ModelName => _completion.ModelName;

    public async Task<AiSuggestionDraft> SuggestAsync(AiSuggestionRequest request, CancellationToken cancellationToken)
    {
        string content;
        try
        {
            content = await _completion.CompleteAsync(
                AiSuggestionJson.SystemPrompt,
                AiSuggestionJson.BuildUserPrompt(request),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DomainException)
        {
            throw new DomainException(Attempt.SuggestionFailedMessage);
        }

        return AiSuggestionJson.ParseModelContent(content, request.Points);
    }
}
