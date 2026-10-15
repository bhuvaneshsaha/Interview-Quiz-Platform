using System.Text.Json;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Evaluation.Domain;

namespace InterviewQuiz.Evaluation.Application.Scoring;

public sealed record QuestionScore(
    Guid QuestionId,
    int SortOrder,
    string Type,
    string ScoringMode,
    int Points,
    ItemScoreStatus Status,
    decimal? PointsAwarded);

public static class AutoScorer
{
    public static IReadOnlyList<QuestionScore> ScoreAttempt(
        QuizSnapshotDto snapshot,
        IReadOnlyDictionary<Guid, JsonElement> answers)
    {
        var results = new List<QuestionScore>(snapshot.Questions.Count);
        foreach (var question in snapshot.Questions.OrderBy(q => q.SortOrder).ThenBy(q => q.Id))
        {
            answers.TryGetValue(question.Id, out var answer);
            var hasAnswer = answers.ContainsKey(question.Id);
            results.Add(ScoreQuestion(question, hasAnswer ? answer : null));
        }

        return results;
    }

    public static QuestionScore ScoreQuestion(QuizSnapshotQuestionDto question, JsonElement? answer)
    {
        var type = CatalogJson.ToCamelCase(question.Type);
        var scoring = CatalogJson.ToCamelCase(question.ScoringMode);

        if (question.ScoringMode != ScoringMode.Auto)
        {
            return new QuestionScore(
                question.Id,
                question.SortOrder,
                type,
                scoring,
                question.Points,
                ItemScoreStatus.Unsettled,
                null);
        }

        var awarded = ScoreAuto(question, answer);
        return new QuestionScore(
            question.Id,
            question.SortOrder,
            type,
            scoring,
            question.Points,
            ItemScoreStatus.Scored,
            awarded);
    }

    private static decimal ScoreAuto(QuizSnapshotQuestionDto question, JsonElement? answer)
    {
        if (answer is null || answer.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return 0m;
        }

        var value = answer.Value;
        return question.Type switch
        {
            QuestionType.MultipleChoiceSingle => ScoreMcSingle(question, value),
            QuestionType.MultipleChoiceMulti => ScoreMcMulti(question, value),
            QuestionType.TrueFalse => ScoreTrueFalse(question, value),
            QuestionType.ShortText => ScoreShortText(question, value),
            QuestionType.Ordering => ScoreOrdering(question, value),
            QuestionType.DragDropSharedBank => ScoreDragShared(question, value),
            QuestionType.DragDropPerSlot => ScoreDragPerSlot(question, value),
            _ => 0m
        };
    }

    private static decimal ScoreMcSingle(QuizSnapshotQuestionDto question, JsonElement value)
    {
        if (!TryGetString(value, "optionId", out var selected))
        {
            return 0m;
        }

        var correct = CorrectOptionIds(question.Body);
        return correct.Count == 1 && correct.Contains(selected) ? question.Points : 0m;
    }

    private static decimal ScoreMcMulti(QuizSnapshotQuestionDto question, JsonElement value)
    {
        var selected = ReadStringArray(value, "optionIds");
        var key = CorrectOptionIds(question.Body);
        if (key.Count == 0)
        {
            return 0m;
        }

        var credit = question.CreditMode ?? CreditMode.AllOrNothing;
        if (credit == CreditMode.AllOrNothing)
        {
            return selected.SetEquals(key) ? question.Points : 0m;
        }

        var hits = selected.Intersect(key, StringComparer.Ordinal).Count();
        return question.Points * ((decimal)hits / key.Count);
    }

    private static decimal ScoreTrueFalse(QuizSnapshotQuestionDto question, JsonElement value)
    {
        if (!TryGetBool(value, "value", out var selected)
            || !TryGetBool(question.Body, "correct", out var correct))
        {
            return 0m;
        }

        return selected == correct ? question.Points : 0m;
    }

    private static decimal ScoreShortText(QuizSnapshotQuestionDto question, JsonElement value)
    {
        if (!TryGetString(value, "text", out var raw))
        {
            return 0m;
        }

        var text = raw.Trim();
        var caseSensitive = TryGetBool(question.Body, "caseSensitive", out var cs) && cs;
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (!question.Body.TryGetProperty("acceptableAnswers", out var answers)
            || answers.ValueKind != JsonValueKind.Array)
        {
            return 0m;
        }

        foreach (var item in answers.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String
                && string.Equals(item.GetString()?.Trim(), text, comparison))
            {
                return question.Points;
            }
        }

        return 0m;
    }

    private static decimal ScoreOrdering(QuizSnapshotQuestionDto question, JsonElement value)
    {
        var candidate = ReadStringArray(value, "itemIds").ToList();
        var key = KeyedOrder(question.Body);
        if (key.Count < 2 || candidate.Count != key.Count)
        {
            return 0m;
        }

        if (candidate.Distinct(StringComparer.Ordinal).Count() != candidate.Count)
        {
            return 0m;
        }

        if (!candidate.ToHashSet(StringComparer.Ordinal).SetEquals(key))
        {
            return 0m;
        }

        var credit = question.CreditMode ?? CreditMode.AllOrNothing;
        if (credit == CreditMode.AllOrNothing)
        {
            return candidate.SequenceEqual(key, StringComparer.Ordinal) ? question.Points : 0m;
        }

        var pairs = key.Count - 1;
        if (pairs <= 0)
        {
            return 0m;
        }

        var hits = 0;
        for (var i = 0; i < pairs; i++)
        {
            if (string.Equals(candidate[i], key[i], StringComparison.Ordinal)
                && string.Equals(candidate[i + 1], key[i + 1], StringComparison.Ordinal))
            {
                hits++;
            }
        }

        return question.Points * ((decimal)hits / pairs);
    }

    private static decimal ScoreDragShared(QuizSnapshotQuestionDto question, JsonElement value)
    {
        var answers = SlotMap(value, "itemId");
        if (!question.Body.TryGetProperty("slots", out var slots) || slots.ValueKind != JsonValueKind.Array)
        {
            return 0m;
        }

        foreach (var slot in slots.EnumerateArray())
        {
            if (!TryGetString(slot, "id", out var slotId)
                || !TryGetString(slot, "correctItemId", out var correct))
            {
                return 0m;
            }

            if (!answers.TryGetValue(slotId, out var itemId)
                || !string.Equals(itemId, correct, StringComparison.Ordinal))
            {
                return 0m;
            }
        }

        return question.Points;
    }

    private static decimal ScoreDragPerSlot(QuizSnapshotQuestionDto question, JsonElement value)
    {
        var answers = SlotMap(value, "optionId");
        if (!question.Body.TryGetProperty("slots", out var slots) || slots.ValueKind != JsonValueKind.Array)
        {
            return 0m;
        }

        foreach (var slot in slots.EnumerateArray())
        {
            if (!TryGetString(slot, "id", out var slotId))
            {
                return 0m;
            }

            var correct = CorrectOptionIds(slot);
            if (correct.Count != 1
                || !answers.TryGetValue(slotId, out var optionId)
                || !correct.Contains(optionId))
            {
                return 0m;
            }
        }

        return question.Points;
    }

    private static HashSet<string> CorrectOptionIds(JsonElement body)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (!body.TryGetProperty("options", out var options) || options.ValueKind != JsonValueKind.Array)
        {
            return ids;
        }

        foreach (var option in options.EnumerateArray())
        {
            if (TryGetBool(option, "isCorrect", out var correct)
                && correct
                && TryGetString(option, "id", out var id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    private static List<string> KeyedOrder(JsonElement body)
    {
        if (!body.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return items.EnumerateArray()
            .Select(item =>
            {
                TryGetString(item, "id", out var id);
                var index = item.TryGetProperty("correctIndex", out var idx) && idx.TryGetInt32(out var n) ? n : int.MaxValue;
                return (Id: id ?? "", Index: index);
            })
            .Where(item => item.Id.Length > 0)
            .OrderBy(item => item.Index)
            .Select(item => item.Id)
            .ToList();
    }

    private static Dictionary<string, string> SlotMap(JsonElement value, string idProperty)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!value.TryGetProperty("slots", out var slots) || slots.ValueKind != JsonValueKind.Array)
        {
            return map;
        }

        foreach (var slot in slots.EnumerateArray())
        {
            if (TryGetString(slot, "slotId", out var slotId) && TryGetString(slot, idProperty, out var id))
            {
                map[slotId] = id;
            }
        }

        return map;
    }

    private static HashSet<string> ReadStringArray(JsonElement value, string name)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (!value.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return set;
        }

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
            {
                set.Add(item.GetString()!);
            }
        }

        return set;
    }

    private static bool TryGetString(JsonElement element, string name, out string value)
    {
        value = "";
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = property.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        value = text;
        return true;
    }

    private static bool TryGetBool(JsonElement element, string name, out bool value)
    {
        value = false;
        if (!element.TryGetProperty(name, out var property))
        {
            return false;
        }

        if (property.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            value = property.GetBoolean();
            return true;
        }

        return false;
    }
}
