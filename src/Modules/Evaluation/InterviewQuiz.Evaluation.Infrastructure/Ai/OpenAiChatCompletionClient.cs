using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InterviewQuiz.Kernel.Ai;
using InterviewQuiz.Kernel.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InterviewQuiz.Evaluation.Infrastructure.Ai;

public sealed class OpenAiChatCompletionClient : IAiCompletionClient
{
    private readonly HttpClient _http;
    private readonly ILogger<OpenAiChatCompletionClient> _logger;
    private readonly string _model;

    public OpenAiChatCompletionClient(
        HttpClient http,
        IConfiguration configuration,
        ILogger<OpenAiChatCompletionClient> logger)
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

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        var payload = new
        {
            model = _model,
            temperature = 0,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };

        try
        {
            using var response = await _http.PostAsJsonAsync("chat/completions", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AI completion request failed with status {StatusCode} for model {Model}",
                    (int)response.StatusCode,
                    _model);
                throw new DomainException(AiCompletionMessages.Failed);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return ReadMessageContent(body);
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
                "AI completion request could not be completed for model {Model}",
                _model);
            throw new DomainException(AiCompletionMessages.Failed);
        }
    }

    private static string ReadMessageContent(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var content = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new DomainException(AiCompletionMessages.Failed);
            }

            return content;
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            throw new DomainException(AiCompletionMessages.Failed);
        }
    }
}
