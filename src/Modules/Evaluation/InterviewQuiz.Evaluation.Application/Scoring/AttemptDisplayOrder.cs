using System.Text.Json;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;

namespace InterviewQuiz.Evaluation.Application.Scoring;

public sealed record AttemptDisplayPlan(int Seed, JsonDocument Order);

/// <summary>
/// Builds a display-only question and multiple-choice option order once per attempt.
/// Ordering items, slots, and banks stay on the existing prompt-order path.
/// </summary>
public static class AttemptDisplayOrder
{
    public static AttemptDisplayPlan Create(QuizSnapshotDto snapshot, int? seed = null)
    {
        var resolved = seed ?? Random.Shared.Next();
        var random = new Random(resolved);
        var questions = snapshot.Questions.OrderBy(q => q.SortOrder).ThenBy(q => q.Id).ToList();
        var questionIds = questions.Select(q => q.Id).ToList();
        if (snapshot.ShuffleQuestions)
        {
            Shuffle(questionIds, random);
        }

        var options = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (snapshot.ShuffleOptions)
        {
            foreach (var question in questions)
            {
                if (question.Type is not (QuestionType.MultipleChoiceSingle or QuestionType.MultipleChoiceMulti))
                {
                    continue;
                }

                var ids = ReadOptionIds(question.Body);
                if (ids.Count < 2)
                {
                    continue;
                }

                Shuffle(ids, random);
                options[question.Id.ToString("D")] = ids.ToArray();
            }
        }

        var json = JsonSerializer.Serialize(
            new { questionIds, options },
            CatalogJson.SerializerOptions);
        return new AttemptDisplayPlan(resolved, JsonDocument.Parse(json));
    }

    public static List<QuizSnapshotQuestionDto> OrderQuestions(
        IReadOnlyList<QuizSnapshotQuestionDto> questions,
        JsonDocument? displayOrder)
    {
        var canonical = questions.OrderBy(q => q.SortOrder).ThenBy(q => q.Id).ToList();
        var ids = ParseQuestionIds(displayOrder);
        if (ids.Count == 0)
        {
            return canonical;
        }

        var byId = canonical.ToDictionary(q => q.Id);
        var ordered = new List<QuizSnapshotQuestionDto>(canonical.Count);
        var seen = new HashSet<Guid>();
        foreach (var id in ids)
        {
            if (byId.TryGetValue(id, out var question) && seen.Add(question.Id))
            {
                ordered.Add(question);
            }
        }

        foreach (var question in canonical)
        {
            if (seen.Add(question.Id))
            {
                ordered.Add(question);
            }
        }

        return ordered;
    }

    public static IReadOnlyList<Guid> ParseQuestionIds(JsonDocument? document)
    {
        if (document is null
            || document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("questionIds", out var ids)
            || ids.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<Guid>();
        foreach (var item in ids.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && Guid.TryParse(item.GetString(), out var id))
            {
                result.Add(id);
            }
        }

        return result;
    }

    public static IReadOnlyDictionary<Guid, IReadOnlyList<string>> ParseOptionOrder(JsonDocument? document)
    {
        var result = new Dictionary<Guid, IReadOnlyList<string>>();
        if (document is null
            || document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("options", out var options)
            || options.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var property in options.EnumerateObject())
        {
            if (!Guid.TryParse(property.Name, out var questionId) || property.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var ids = property.Value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!)
                .ToArray();
            if (ids.Length > 0)
            {
                result[questionId] = ids;
            }
        }

        return result;
    }

    private static List<string> ReadOptionIds(JsonElement body)
    {
        var ids = new List<string>();
        if (!body.TryGetProperty("options", out var options) || options.ValueKind != JsonValueKind.Array)
        {
            return ids;
        }

        foreach (var option in options.EnumerateArray())
        {
            if (option.TryGetProperty("id", out var id)
                && id.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(id.GetString()))
            {
                ids.Add(id.GetString()!);
            }
        }

        return ids;
    }

    private static void Shuffle<T>(List<T> items, Random random)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
