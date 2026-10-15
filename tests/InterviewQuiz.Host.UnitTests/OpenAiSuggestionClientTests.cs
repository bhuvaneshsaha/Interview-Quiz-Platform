using System.Net;
using System.Text;
using System.Text.Json;
using InterviewQuiz.Evaluation.Application.Ai;
using InterviewQuiz.Evaluation.Domain;
using InterviewQuiz.Evaluation.Infrastructure.Ai;
using InterviewQuiz.Kernel.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace InterviewQuiz.Host.UnitTests;

public sealed class OpenAiSuggestionClientTests
{
    [Fact]
    public async Task Compatible_client_sends_the_key_as_a_bearer_and_clamps_the_reply()
    {
        string? authorization = null;
        string? body = null;
        var handler = new StubHandler(request =>
        {
            authorization = request.Headers.Authorization?.ToString();
            body = request.Content is null ? null : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var json = """{"choices":[{"message":{"content":"{\"points\":40,\"feedback\":\"Covers the boundary.\"}"}}]}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ai:BaseUrl"] = "https://llm.example/v1",
            ["Ai:Model"] = "local-model",
            ["Ai:ApiKey"] = "super-secret-key-value"
        }).Build();
        var client = SuggestionClient(new HttpClient(handler), configuration);

        var draft = await client.SuggestAsync(
            new AiSuggestionRequest(
                Guid.NewGuid(),
                "longText",
                10,
                "Describe the boundary.",
                JsonDocument.Parse("""{"guidance":"Name the API."}""").RootElement,
                JsonDocument.Parse("""{"text":"The public API."}""").RootElement),
            CancellationToken.None);

        Assert.Equal("Bearer super-secret-key-value", authorization);
        Assert.NotNull(body);
        Assert.DoesNotContain("super-secret-key-value", body, StringComparison.Ordinal);
        Assert.Contains("local-model", body, StringComparison.Ordinal);
        Assert.Contains("Describe the boundary.", body, StringComparison.Ordinal);
        Assert.Equal(10m, draft.Points);
        Assert.Equal("Covers the boundary.", draft.Feedback);
        Assert.Equal("https://llm.example/v1/chat/completions", handler.LastRequestUri?.ToString());
    }

    [Fact]
    public async Task Upstream_failure_does_not_include_the_response_body()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("super-secret-key-value leaked")
        });
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ai:BaseUrl"] = "https://llm.example/v1",
            ["Ai:Model"] = "local-model",
            ["Ai:ApiKey"] = "super-secret-key-value"
        }).Build();
        var client = SuggestionClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://llm.example/v1/") },
            configuration);

        var exception = await Assert.ThrowsAsync<DomainException>(() => client.SuggestAsync(
            new AiSuggestionRequest(Guid.NewGuid(), "longText", 5, "Stem", default, null),
            CancellationToken.None));

        Assert.Equal(Attempt.SuggestionFailedMessage, exception.Message);
        Assert.DoesNotContain("super-secret-key-value", exception.Message, StringComparison.Ordinal);
    }

    private static OpenAiCompatibleSuggestionClient SuggestionClient(HttpClient http, IConfiguration configuration)
    {
        var completion = new OpenAiChatCompletionClient(
            http,
            configuration,
            NullLogger<OpenAiChatCompletionClient>.Instance);
        return new OpenAiCompatibleSuggestionClient(completion);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(_respond(request));
        }
    }
}
