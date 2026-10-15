using System.Text.Json;
using System.Text.Json.Nodes;
using InterviewQuiz.Kernel.Exceptions;

namespace InterviewQuiz.Kernel.Ai;

public interface IAiCompletionClient
{
    bool IsEnabled { get; }

    string ModelName { get; }

    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken);
}

public static class AiCompletionMessages
{
    public const string Unavailable = "AI drafts are not configured.";

    public const string Failed = "AI completion could not be produced.";
}

public sealed class DisabledAiCompletionClient : IAiCompletionClient
{
    public static readonly DisabledAiCompletionClient Instance = new();

    public bool IsEnabled => false;

    public string ModelName => "";

    public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
        => throw new DomainException(AiCompletionMessages.Unavailable);
}

/// <summary>
/// Shared authoring prompt lines. The preamble ends at a line <c>Resume:</c> so resume text
/// cannot be read as rules or template questions.
/// </summary>
public static class AiDraftPrompt
{
    public const string OpeningTitlePrefix = "Opening title: ";
    public const string ExperiencePrefix = "Expected experience years: ";
    public const string ResumePresentPrefix = "Resume present: ";
    public const string RulesPrefix = "Rules: ";
    public const string TemplateQuestionPrefix = "TEMPLATE_QUESTION ";
    public const string ResumeHeader = "Resume:";
    public const int MaxQuestions = 40;

    public const string SystemPrompt =
        "You draft a quiz a human must edit before it is saved. Reply with a JSON object {\"title\": string, \"questions\": [{\"type\": string, \"stem\": string, \"scoringMode\": string, \"points\": number, \"body\": object}]} and nothing else. Do not assign the quiz.";
}

public sealed class FakeAiCompletionClient : IAiCompletionClient
{
    public const string ModelId = "fake";
    public const string ReviewGuidance = "Review before use.";

    public bool IsEnabled => true;

    public string ModelName => ModelId;

    public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
        => Task.FromResult(BuildAuthoringDraft(userPrompt));

    public static string BuildAuthoringDraft(string userPrompt)
    {
        var preamble = Preamble(userPrompt);
        var title = ReadLineValue(preamble, AiDraftPrompt.OpeningTitlePrefix);
        if (string.IsNullOrWhiteSpace(title))
        {
            title = "Opening";
        }

        if (title.Length > 180)
        {
            title = title[..180];
        }

        var resumePresent = string.Equals(
            ReadLineValue(preamble, AiDraftPrompt.ResumePresentPrefix),
            "yes",
            StringComparison.OrdinalIgnoreCase);
        var rules = ReadLineValue(preamble, AiDraftPrompt.RulesPrefix);
        var guidance = string.IsNullOrWhiteSpace(rules)
            || string.Equals(rules, "none", StringComparison.OrdinalIgnoreCase)
            ? ReviewGuidance
            : rules.Length > 8000 ? rules[..8000] : rules;

        var questions = new JsonArray
        {
            new JsonObject
            {
                ["type"] = "trueFalse",
                ["stem"] = "Draft check for " + title + ".",
                ["scoringMode"] = "auto",
                ["points"] = 1,
                ["body"] = new JsonObject { ["correct"] = true }
            },
            new JsonObject
            {
                ["type"] = "longText",
                ["stem"] = resumePresent
                    ? "Draft written answer from the resume."
                    : "Draft written answer.",
                ["scoringMode"] = "humanOnly",
                ["points"] = 5,
                ["body"] = new JsonObject { ["guidance"] = guidance }
            }
        };

        foreach (var line in preamble.Split('\n'))
        {
            if (questions.Count >= AiDraftPrompt.MaxQuestions)
            {
                break;
            }

            var trimmed = line.Trim();
            if (!trimmed.StartsWith(AiDraftPrompt.TemplateQuestionPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var json = trimmed[AiDraftPrompt.TemplateQuestionPrefix.Length..];
            try
            {
                if (JsonNode.Parse(json) is JsonObject templateQuestion)
                {
                    questions.Add(templateQuestion);
                }
            }
            catch (JsonException)
            {
                // Skip a template line the prompt could not encode.
            }
        }

        return new JsonObject
        {
            ["title"] = "Draft: " + title,
            ["questions"] = questions
        }.ToJsonString();
    }

    private static string Preamble(string userPrompt)
    {
        var marker = "\n" + AiDraftPrompt.ResumeHeader;
        var index = userPrompt.IndexOf(marker, StringComparison.Ordinal);
        return index >= 0 ? userPrompt[..index] : userPrompt;
    }

    private static string ReadLineValue(string prompt, string prefix)
    {
        foreach (var line in prompt.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                return trimmed[prefix.Length..].Trim();
            }
        }

        return "";
    }
}
