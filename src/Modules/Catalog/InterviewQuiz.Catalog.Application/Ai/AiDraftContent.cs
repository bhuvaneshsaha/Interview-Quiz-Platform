using System.Text;
using System.Text.Json;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Kernel.Ai;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Openings.Application.Contracts;

namespace InterviewQuiz.Catalog.Application.Ai;

internal static class AiDraftPromptBuilder
{
    public static string Build(
        OpeningLookupDto opening,
        string? resume,
        string? rules,
        IReadOnlyList<QuizSnapshotQuestionDto> templateQuestions)
    {
        var builder = new StringBuilder();
        builder.Append(AiDraftPrompt.OpeningTitlePrefix).Append(opening.Title.Trim()).Append('\n');
        builder.Append(AiDraftPrompt.ExperiencePrefix).Append(opening.ExpectedExperienceYears).Append('\n');
        builder.Append(AiDraftPrompt.ResumePresentPrefix).Append(resume is null ? "no" : "yes").Append('\n');
        builder.Append(AiDraftPrompt.RulesPrefix).Append(rules ?? "none").Append('\n');
        var added = 0;
        foreach (var question in templateQuestions)
        {
            if (added >= AiDraftPrompt.MaxQuestions - 2)
            {
                break;
            }

            builder.Append(AiDraftPrompt.TemplateQuestionPrefix)
                .Append(SerializeTemplateQuestion(question))
                .Append('\n');
            added++;
        }

        builder.Append(AiDraftPrompt.ResumeHeader).Append('\n');
        if (resume is not null)
        {
            builder.Append(resume).Append('\n');
        }

        builder.Append("Return JSON only: {\"title\": string, \"questions\":[{\"type\": string, \"stem\": string, \"scoringMode\": string, \"points\": number, \"body\": object}]}. At most ")
            .Append(AiDraftPrompt.MaxQuestions)
            .Append(" questions. Include every TEMPLATE_QUESTION. A human edits this draft before it is saved. Do not assign it.");
        return builder.ToString();
    }

    public static string? NormalizeOptional(string? value, int max, string tooLongMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > max)
        {
            throw new DomainException(tooLongMessage);
        }

        return trimmed;
    }

    public static string CollapseRules(string rules)
        => rules.Replace('\r', ' ').Replace('\n', ' ');

    private static string SerializeTemplateQuestion(QuizSnapshotQuestionDto question)
    {
        var payload = new Dictionary<string, object?>
        {
            ["type"] = CatalogJson.ToCamelCase(question.Type),
            ["stem"] = question.Stem,
            ["scoringMode"] = CatalogJson.ToCamelCase(question.ScoringMode),
            ["points"] = question.Points,
            ["body"] = question.Body
        };
        if (question.CreditMode is { } credit)
        {
            payload["creditMode"] = CatalogJson.ToCamelCase(credit);
        }

        return JsonSerializer.Serialize(payload, CatalogJson.SerializerOptions);
    }
}

internal static class AiDraftContent
{
    public static (string Title, List<Question> Questions, string CanonicalJson) Read(string content)
    {
        var trimmed = StripFence(content.Trim());
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(trimmed);
        }
        catch (JsonException)
        {
            throw new DomainException(AiDraft.FailedMessage);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("questions", out var questionsElement)
                || questionsElement.ValueKind != JsonValueKind.Array)
            {
                throw new DomainException(AiDraft.FailedMessage);
            }

            var title = root.TryGetProperty("title", out var titleElement)
                && titleElement.ValueKind == JsonValueKind.String
                ? titleElement.GetString()?.Trim() ?? ""
                : "";

            var questions = new List<Question>();
            var index = 0;
            foreach (var item in questionsElement.EnumerateArray())
            {
                if (questions.Count >= AiDraftPrompt.MaxQuestions)
                {
                    break;
                }

                questions.Add(ReadQuestion(item, index));
                index++;
            }

            if (questions.Count == 0)
            {
                throw new DomainException(AiDraft.FailedMessage);
            }

            return (title, questions, Canonical(questions));
        }
    }

    public static string Canonical(IReadOnlyList<Question> questions)
    {
        var payload = questions.Select(question => new CanonicalDraftQuestion
        {
            Type = CatalogJson.ToCamelCase(question.Type),
            Stem = question.Stem,
            ScoringMode = CatalogJson.ToCamelCase(question.ScoringMode),
            CreditMode = question.CreditMode is null ? null : CatalogJson.ToCamelCase(question.CreditMode.Value),
            Points = question.Points,
            Body = question.Body.RootElement.Clone()
        }).ToList();
        return JsonSerializer.Serialize(payload, CatalogJson.SerializerOptions);
    }

    public static bool SameQuestions(string storedCanonical, string incomingCanonical)
        => string.Equals(storedCanonical, incomingCanonical, StringComparison.Ordinal);

    private static Question ReadQuestion(JsonElement item, int index)
    {
        try
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new DomainException(AiDraft.FailedMessage);
            }

            var type = JsonSerializer.Deserialize<QuestionType>(
                item.GetProperty("type").GetRawText(),
                CatalogJson.SerializerOptions);
            var stem = item.GetProperty("stem").GetString() ?? "";
            var points = ReadPoints(item.GetProperty("points"));
            ScoringMode? scoring = null;
            if (item.TryGetProperty("scoringMode", out var scoringElement)
                && scoringElement.ValueKind == JsonValueKind.String)
            {
                scoring = JsonSerializer.Deserialize<ScoringMode>(scoringElement.GetRawText(), CatalogJson.SerializerOptions);
            }

            CreditMode? credit = null;
            if (item.TryGetProperty("creditMode", out var creditElement)
                && creditElement.ValueKind == JsonValueKind.String)
            {
                credit = JsonSerializer.Deserialize<CreditMode>(creditElement.GetRawText(), CatalogJson.SerializerOptions);
            }

            var body = item.TryGetProperty("body", out var bodyElement) ? bodyElement : default;
            return Question.Create(null, index, type, stem, scoring, credit, points, body);
        }
        catch (DomainException exception) when (exception.Message == AiDraft.FailedMessage)
        {
            throw;
        }
        catch (Exception exception) when (exception is DomainException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new DomainException(AiDraft.FailedMessage);
        }
    }

    private static int ReadPoints(JsonElement points)
    {
        if (points.TryGetInt32(out var whole))
        {
            return whole;
        }

        if (points.TryGetDecimal(out var value) && value == decimal.Truncate(value) && value is >= 1 and <= 10_000)
        {
            return (int)value;
        }

        throw new DomainException(AiDraft.FailedMessage);
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

    private sealed class CanonicalDraftQuestion
    {
        public string Type { get; set; } = "";
        public string Stem { get; set; } = "";
        public string ScoringMode { get; set; } = "";
        public string? CreditMode { get; set; }
        public int Points { get; set; }
        public JsonElement Body { get; set; }
    }
}
