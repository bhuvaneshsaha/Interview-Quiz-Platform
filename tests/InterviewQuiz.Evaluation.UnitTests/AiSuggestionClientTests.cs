using System.Text.Json;
using InterviewQuiz.Evaluation.Application.Ai;
using InterviewQuiz.Evaluation.Application.Services;
using InterviewQuiz.Evaluation.Domain;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;

namespace InterviewQuiz.Evaluation.UnitTests;

public sealed class AiSuggestionClientTests
{
    [Theory]
    [InlineData(null, null, null, null, AiSuggestionMode.None)]
    [InlineData("None", "https://llm.example/v1", "m", "key", AiSuggestionMode.None)]
    [InlineData("Fake", null, null, null, AiSuggestionMode.Fake)]
    [InlineData(" fake ", "", "", "", AiSuggestionMode.Fake)]
    [InlineData("OpenAiCompatible", "https://llm.example/v1", "gpt", "", AiSuggestionMode.None)]
    [InlineData("OpenAiCompatible", "not-a-url", "gpt", "key", AiSuggestionMode.None)]
    [InlineData("OpenAiCompatible", "https://llm.example/v1", "gpt", "key", AiSuggestionMode.OpenAiCompatible)]
    public void Mode_is_fake_or_a_fully_configured_endpoint_or_off(
        string? provider,
        string? baseUrl,
        string? model,
        string? apiKey,
        string expected)
    {
        Assert.Equal(expected, AiSuggestionMode.Resolve(provider, baseUrl, model, apiKey));
    }

    [Fact]
    public async Task Fake_scores_half_for_text_and_zero_for_an_empty_answer()
    {
        var client = new FakeAiSuggestionClient();
        var empty = await client.SuggestAsync(Request(JsonDocument.Parse("""{"text":"  "}""").RootElement), CancellationToken.None);
        Assert.Equal(0, empty.Points);
        Assert.Equal(FakeAiSuggestionClient.EmptyFeedback, empty.Feedback);

        var text = await client.SuggestAsync(Request(JsonDocument.Parse("""{"text":"The review screen."}""").RootElement), CancellationToken.None);
        Assert.Equal(5m, text.Points);
        Assert.Equal(FakeAiSuggestionClient.HalfPointsFeedback, text.Feedback);
        Assert.Equal("fake", client.ModelName);
    }

    [Fact]
    public void Parser_clamps_points_and_reads_fenced_json()
    {
        var fenced = """
            ```json
            {"points": 12.5, "feedback": "Too high."}
            ```
            """;
        var draft = AiSuggestionJson.ParseModelContent(fenced, 10);
        Assert.Equal(10m, draft.Points);
        Assert.Equal("Too high.", draft.Feedback);

        var low = AiSuggestionJson.ParseModelContent("""{"points": -3, "feedback": " "}""", 10);
        Assert.Equal(0m, low.Points);
        Assert.Equal("", low.Feedback);

        var bad = Assert.Throws<DomainException>(() => AiSuggestionJson.ParseModelContent("not json", 10));
        Assert.Equal(Attempt.SuggestionFailedMessage, bad.Message);
    }

    [Fact]
    public void Chat_response_without_content_is_rejected()
    {
        var missing = Assert.Throws<DomainException>(() => AiSuggestionJson.ReadMessageContent("""{"choices":[]}"""));
        Assert.Equal(Attempt.SuggestionFailedMessage, missing.Message);

        var content = AiSuggestionJson.ReadMessageContent(
            """{"choices":[{"message":{"content":"{\"points\":1,\"feedback\":\"ok\"}"}}]}""");
        Assert.Contains("\"points\":1", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unconfigured_suggest_fails_before_loading_an_attempt()
    {
        var service = new AttemptService(
            null!,
            null!,
            null!,
            new FixedClock(DateTimeOffset.UnixEpoch),
            NullLogger<AttemptService>.Instance);

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            service.SuggestItemAsync(Guid.NewGuid(), Guid.NewGuid(), "reviewer", CancellationToken.None));
        Assert.Equal(Attempt.SuggestionUnavailableMessage, exception.Message);
    }

    private static AiSuggestionRequest Request(JsonElement answer)
        => new(Guid.NewGuid(), "longText", 10, "Stem", JsonDocument.Parse("{}").RootElement, answer);

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
