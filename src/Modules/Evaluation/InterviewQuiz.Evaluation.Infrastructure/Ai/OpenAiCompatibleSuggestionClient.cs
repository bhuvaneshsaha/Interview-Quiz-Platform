using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InterviewQuiz.Evaluation.Application.Ai;
using InterviewQuiz.Evaluation.Domain;
using InterviewQuiz.Kernel.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InterviewQuiz.Evaluation.Infrastructure.Ai;

public sealed class OpenAiCompatibleSuggestionClient : IAiSuggestionClient
{
    private readonly HttpClient _http;
    private readonly ILogger<OpenAiCompatibleSuggestionClient> _logger;
    private readonly string _model;

    public OpenAiCompatibleSuggestionClient(
        HttpClient http,
        IConfiguration configuration,
        ILogger<OpenAiCompatibleSuggestionClient> logger)
    {
        _http = http;
        _logger = logger;
        _model = configuration["Ai:Model"]?.Trim() ?? "";
        var apiKey = configuration["Ai:ApiKey"]?.Trim() ?? "";
        if (apiKey.Length > 0 && _http.DefaultRequestHeaders.Authorization is null)
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        if (_http.BaseAddress is null
            && Uri.TryCreate(configuration["Ai:BaseUrl"]?.Trim(), UriKind.Absolute, out var baseUri))
        {
            var text = baseUri.ToString().TrimEnd('/') + "/";
            _http.BaseAddress = new Uri(text);
        }

        if (int.TryParse(configuration["Ai:TimeoutSeconds"], out var seconds) && seconds > 0)
        {
            _http.Timeout = TimeSpan.FromSeconds(Math.Min(seconds, 120));
        }
    }

    public bool IsEnabled => true;

    public string ModelName => _model;

    public async Task<AiSuggestionDraft> SuggestAsync(AiSuggestionRequest request, CancellationToken cancellationToken)
    {
        var payload = new
        {
            model = _model,
            temperature = 0,
            messages = new object[]
            {
                new { role = "system", content = AiSuggestionJson.SystemPrompt },
                new { role = "user", content = AiSuggestionJson.BuildUserPrompt(request) }
            }
        };

        try
        {
            using var response = await _http.PostAsJsonAsync("chat/completions", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AI suggestion request failed with status {StatusCode} for model {Model}",
                    (int)response.StatusCode,
                    _model);
                throw new DomainException(Attempt.SuggestionFailedMessage);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var content = AiSuggestionJson.ReadMessageContent(body);
            return AiSuggestionJson.ParseModelContent(content, request.Points);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(
                "AI suggestion request could not be completed for model {Model}",
                _model);
            throw new DomainException(Attempt.SuggestionFailedMessage);
        }
    }
}
