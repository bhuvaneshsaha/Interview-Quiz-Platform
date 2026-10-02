using System.Text.Json;
using System.Text.Json.Nodes;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;

namespace InterviewQuiz.Evaluation.Application.Scoring;

public static class CandidateQuestionRedactor
{
    public static JsonElement RedactBody(
        QuizSnapshotQuestionDto question,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> promptOrder)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(question.Body.GetRawText());
        }
        catch (JsonException)
        {
            node = new JsonObject();
        }

        node ??= new JsonObject();
        if (node is not JsonObject obj)
        {
            obj = new JsonObject();
        }

        switch (question.Type)
        {
            case QuestionType.MultipleChoiceSingle:
            case QuestionType.MultipleChoiceMulti:
                StripOptionFlags(obj["options"] as JsonArray);
                break;
            case QuestionType.TrueFalse:
                obj.Remove("correct");
                break;
            case QuestionType.ShortText:
                obj.Remove("acceptableAnswers");
                break;
            case QuestionType.DragDropSharedBank:
                StripSharedSlots(obj["slots"] as JsonArray);
                StripDistractorFlags(obj["bank"] as JsonArray);
                break;
            case QuestionType.DragDropPerSlot:
                StripPerSlotOptions(obj["slots"] as JsonArray);
                break;
            case QuestionType.Ordering:
                ReorderOrderingItems(obj, question.Id, promptOrder);
                break;
        }

        using var document = JsonDocument.Parse(obj.ToJsonString(CatalogJson.SerializerOptions));
        return document.RootElement.Clone();
    }

    public static JsonDocument BuildPromptOrder(QuizSnapshotDto snapshot)
    {
        var map = new Dictionary<string, string[]>();
        foreach (var question in snapshot.Questions)
        {
            if (question.Type != QuestionType.Ordering)
            {
                continue;
            }

            var ids = ReadItemIds(question.Body);
            Shuffle(ids);
            map[question.Id.ToString("D")] = ids.ToArray();
        }

        var json = JsonSerializer.Serialize(map, CatalogJson.SerializerOptions);
        return JsonDocument.Parse(json);
    }

    public static IReadOnlyDictionary<Guid, IReadOnlyList<string>> ParsePromptOrder(JsonDocument document)
    {
        var result = new Dictionary<Guid, IReadOnlyList<string>>();
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!Guid.TryParse(property.Name, out var questionId)
                || property.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var ids = property.Value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()!)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToArray();
            result[questionId] = ids;
        }

        return result;
    }

    private static void StripOptionFlags(JsonArray? options)
    {
        if (options is null)
        {
            return;
        }

        foreach (var option in options.OfType<JsonObject>())
        {
            option.Remove("isCorrect");
        }
    }

    private static void StripSharedSlots(JsonArray? slots)
    {
        if (slots is null)
        {
            return;
        }

        foreach (var slot in slots.OfType<JsonObject>())
        {
            slot.Remove("correctItemId");
        }
    }

    private static void StripDistractorFlags(JsonArray? bank)
    {
        if (bank is null)
        {
            return;
        }

        foreach (var item in bank.OfType<JsonObject>())
        {
            item.Remove("isDistractor");
        }
    }

    private static void StripPerSlotOptions(JsonArray? slots)
    {
        if (slots is null)
        {
            return;
        }

        foreach (var slot in slots.OfType<JsonObject>())
        {
            StripOptionFlags(slot["options"] as JsonArray);
        }
    }

    private static void ReorderOrderingItems(
        JsonObject body,
        Guid questionId,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> promptOrder)
    {
        if (body["items"] is not JsonArray items)
        {
            return;
        }

        var byId = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var item in items.OfType<JsonObject>())
        {
            var id = item["id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            item.Remove("correctIndex");
            byId[id] = item;
        }

        var order = promptOrder.TryGetValue(questionId, out var stored)
            ? stored
            : byId.Keys.ToArray();

        var reordered = new JsonArray();
        foreach (var id in order)
        {
            if (byId.TryGetValue(id, out var item))
            {
                reordered.Add(item.DeepClone());
            }
        }

        foreach (var pair in byId)
        {
            if (order.All(id => !string.Equals(id, pair.Key, StringComparison.Ordinal)))
            {
                reordered.Add(pair.Value.DeepClone());
            }
        }

        body["items"] = reordered;
    }

    private static List<string> ReadItemIds(JsonElement body)
    {
        var ids = new List<string>();
        if (!body.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return ids;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("id", out var idEl)
                && idEl.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(idEl.GetString()))
            {
                ids.Add(idEl.GetString()!);
            }
        }

        return ids;
    }

    private static void Shuffle(List<string> ids)
    {
        if (ids.Count < 2)
        {
            return;
        }

        // Do not sort by keyed order. Fisher–Yates so refresh uses the stored promptOrder, not this shuffle.
        for (var i = ids.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (ids[i], ids[j]) = (ids[j], ids[i]);
        }
    }
}
