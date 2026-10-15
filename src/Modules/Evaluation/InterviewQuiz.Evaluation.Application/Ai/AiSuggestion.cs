using System.Text.Json;
using InterviewQuiz.Evaluation.Domain;
using InterviewQuiz.Kernel.Exceptions;

namespace InterviewQuiz.Evaluation.Application.Ai;

public sealed record AiSuggestionRequest(
    Guid QuestionId,
    string Type,
    int Points,
    string Stem,
    JsonElement AnswerKey,
    JsonElement? CandidateAnswer);

public sealed record AiSuggestionDraft(decimal Points, string Feedback);

public interface IAiSuggestionClient
{
    bool IsEnabled { get; }

    string ModelName { get; }

    Task<AiSuggestionDraft> SuggestAsync(AiSuggestionRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Decides whether suggestions run. A missing key or URL disables the feature.
/// </summary>
public static class AiSuggestionMode
{
    public const string None = "None";
    public const string Fake = "Fake";
    public const string OpenAiCompatible = "OpenAiCompatible";

    public static string Resolve(string? provider, string? baseUrl, string? model, string? apiKey)
    {
        if (string.Equals(provider?.Trim(), Fake, StringComparison.OrdinalIgnoreCase))
        {
            return Fake;
        }

        if (string.Equals(provider?.Trim(), OpenAiCompatible, StringComparison.OrdinalIgnoreCase)
            && IsAbsoluteHttp(baseUrl)
            && !string.IsNullOrWhiteSpace(model)
            && !string.IsNullOrWhiteSpace(apiKey))
        {
            return OpenAiCompatible;
        }

        return None;
    }

    public static bool IsAbsoluteHttp(string? baseUrl)
    {
        if (!Uri.TryCreate(baseUrl?.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }
}

public sealed class DisabledAiSuggestionClient : IAiSuggestionClient
{
    public static readonly DisabledAiSuggestionClient Instance = new();

    public bool IsEnabled => false;

    public string ModelName => "";

    public Task<AiSuggestionDraft> SuggestAsync(AiSuggestionRequest request, CancellationToken cancellationToken)
        => throw new DomainException(Attempt.SuggestionUnavailableMessage);
}

/// <summary>
/// Deterministic draft used in Development and tests. Empty answers score 0.
/// Any other answer scores half the item points.
/// </summary>
public sealed class FakeAiSuggestionClient : IAiSuggestionClient
{
    public const string ModelId = "fake";
    public const string EmptyFeedback = "No answer was submitted.";
    public const string HalfPointsFeedback = "Non-empty answer. Suggested half of the available points.";

    public bool IsEnabled => true;

    public string ModelName => ModelId;

    public Task<AiSuggestionDraft> SuggestAsync(AiSuggestionRequest request, CancellationToken cancellationToken)
    {
        var text = CandidateText(request.CandidateAnswer);
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(new AiSuggestionDraft(0, EmptyFeedback));
        }

        var points = decimal.Round(request.Points / 2m, 4, MidpointRounding.AwayFromZero);
        return Task.FromResult(new AiSuggestionDraft(points, HalfPointsFeedback));
    }

    public static string CandidateText(JsonElement? answer)
    {
        if (answer is not { } value || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return "";
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString()?.Trim() ?? "";
        }

        if (value.ValueKind == JsonValueKind.Object
            && value.TryGetProperty("text", out var text)
            && text.ValueKind == JsonValueKind.String)
        {
            return text.GetString()?.Trim() ?? "";
        }

        var raw = value.GetRawText().Trim();
        return raw is "{}" or "[]" or "null" ? "" : raw;
    }
}

public static class AiSuggestionJson
{
    public const string SystemPrompt =
        "You suggest a score for one quiz answer. A human confirms or edits it. Reply with a JSON object {\"points\": number, \"feedback\": string} and nothing else.";

    public static string BuildUserPrompt(AiSuggestionRequest request)
    {
        var answer = request.CandidateAnswer is { } value
            && value.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null
            ? value.GetRawText()
            : "null";
        var key = request.AnswerKey.ValueKind is JsonValueKind.Undefined ? "null" : request.AnswerKey.GetRawText();
        return
            "Score this one answer. Maximum points: " + request.Points + ".\n" +
            "Question type: " + request.Type + "\n" +
            "Stem: " + request.Stem + "\n" +
            "Answer key JSON: " + key + "\n" +
            "Candidate answer JSON: " + answer + "\n" +
            "Return JSON only: {\"points\": number, \"feedback\": string}\n" +
            "points must be between 0 and " + request.Points + ".";
    }

    public static string ReadMessageContent(string responseBody)
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
                throw new DomainException(Attempt.SuggestionFailedMessage);
            }

            return content;
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            throw new DomainException(Attempt.SuggestionFailedMessage);
        }
    }

    public static AiSuggestionDraft ParseModelContent(string content, int maxPoints)
    {
        var trimmed = StripFence(content.Trim());
        try
        {
            using var document = JsonDocument.Parse(trimmed);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("points", out var pointsElement)
                || !pointsElement.TryGetDecimal(out var points))
            {
                throw new DomainException(Attempt.SuggestionFailedMessage);
            }

            var feedback = root.TryGetProperty("feedback", out var feedbackElement)
                && feedbackElement.ValueKind == JsonValueKind.String
                ? feedbackElement.GetString() ?? ""
                : "";
            return Clamp(points, feedback, maxPoints);
        }
        catch (DomainException)
        {
            throw;
        }
        catch (JsonException)
        {
            throw new DomainException(Attempt.SuggestionFailedMessage);
        }
    }

    public static AiSuggestionDraft Clamp(decimal points, string feedback, int maxPoints)
    {
        var awarded = decimal.Round(points, 4, MidpointRounding.AwayFromZero);
        if (awarded < 0)
        {
            awarded = 0;
        }

        if (awarded > maxPoints)
        {
            awarded = maxPoints;
        }

        var note = string.IsNullOrWhiteSpace(feedback) ? "" : feedback.Trim();
        if (note.Length > Attempt.SuggestionFeedbackMaxLength)
        {
            note = note[..Attempt.SuggestionFeedbackMaxLength];
        }

        return new AiSuggestionDraft(awarded, note);
    }

    private static string StripFence(string content)
    {
        if (!content.StartsWith("```", StringComparison.Ordinal))
        {
            return content;
        }

        var firstLine = content.IndexOf('\n');
        if (firstLine < 0)
        {
            return content;
        }

        var body = content[(firstLine + 1)..];
        var close = body.LastIndexOf("```", StringComparison.Ordinal);
        return close >= 0 ? body[..close].Trim() : body.Trim();
    }
}
