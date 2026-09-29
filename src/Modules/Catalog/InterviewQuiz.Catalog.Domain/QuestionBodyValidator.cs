using System.Text.Json;
using InterviewQuiz.Kernel.Exceptions;

namespace InterviewQuiz.Catalog.Domain;

internal static class QuestionBodyValidator
{
    public static JsonDocument Normalize(QuestionType type, JsonElement body, ScoringMode scoring)
    {
        if (body.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new DomainException("Question body is required.");
        }

        if (body.ValueKind != JsonValueKind.Object)
        {
            throw new DomainException("Question body must be a JSON object.");
        }

        object canonical = type switch
        {
            QuestionType.MultipleChoiceSingle => NormalizeMultipleChoice(body, scoring, singleAnswer: true),
            QuestionType.MultipleChoiceMulti => NormalizeMultipleChoice(body, scoring, singleAnswer: false),
            QuestionType.TrueFalse => NormalizeTrueFalse(body),
            QuestionType.ShortText => NormalizeShortText(body, scoring),
            QuestionType.LongText => NormalizeLongText(body, scoring),
            QuestionType.DragDropSharedBank => NormalizeDragDropSharedBank(body, scoring),
            QuestionType.DragDropPerSlot => NormalizeDragDropPerSlot(body, scoring),
            QuestionType.Ordering => NormalizeOrdering(body),
            _ => throw new DomainException($"Unsupported question type '{CatalogJson.ToCamelCase(type)}'.")
        };

        var json = JsonSerializer.Serialize(canonical, CatalogJson.SerializerOptions);
        return JsonDocument.Parse(json);
    }

    public static ScoringMode DefaultScoring(QuestionType type, JsonElement body)
    {
        return type switch
        {
            QuestionType.MultipleChoiceSingle
                or QuestionType.MultipleChoiceMulti
                or QuestionType.TrueFalse
                or QuestionType.DragDropSharedBank
                or QuestionType.DragDropPerSlot
                or QuestionType.Ordering => ScoringMode.Auto,
            QuestionType.LongText => ScoringMode.HumanOnly,
            QuestionType.ShortText => HasKeyedShortAnswers(body) ? ScoringMode.Auto : ScoringMode.HumanOnly,
            _ => throw new DomainException($"Unsupported question type '{CatalogJson.ToCamelCase(type)}'.")
        };
    }

    private static MultipleChoiceOptionsBody NormalizeMultipleChoice(
        JsonElement body,
        ScoringMode scoring,
        bool singleAnswer)
    {
        var parsed = Deserialize<MultipleChoiceOptionsBody>(body);
        if (parsed.Options is null || parsed.Options.Count < 2)
        {
            throw new DomainException("Multiple choice questions require at least two options.");
        }

        var options = parsed.Options.Select(NormalizeChoice).ToList();
        EnsureUniqueIds(options.Select(o => o.Id), "option");

        var correctCount = options.Count(o => o.IsCorrect);
        if (scoring == ScoringMode.Auto)
        {
            if (singleAnswer && correctCount != 1)
            {
                throw new DomainException(
                    "Multiple choice (single) auto scoring requires exactly one correct option.");
            }

            if (!singleAnswer && correctCount < 1)
            {
                throw new DomainException(
                    "Multiple choice (multi) auto scoring requires at least one correct option.");
            }
        }

        return new MultipleChoiceOptionsBody { Options = options };
    }

    private static TrueFalseBody NormalizeTrueFalse(JsonElement body)
    {
        if (!body.TryGetProperty("correct", out var correctEl)
            || (correctEl.ValueKind is not JsonValueKind.True and not JsonValueKind.False))
        {
            throw new DomainException("True/false questions require a boolean 'correct' value.");
        }

        return new TrueFalseBody { Correct = correctEl.GetBoolean() };
    }

    private static ShortTextBody NormalizeShortText(JsonElement body, ScoringMode scoring)
    {
        var parsed = Deserialize<ShortTextBody>(body);
        var answers = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in parsed.AcceptableAnswers ?? [])
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new DomainException("Acceptable answers cannot be empty.");
            }

            var trimmed = raw.Trim();
            if (!seen.Add(trimmed))
            {
                throw new DomainException("Acceptable answers must be unique.");
            }

            answers.Add(trimmed);
        }

        if (scoring == ScoringMode.Auto && answers.Count == 0)
        {
            throw new DomainException("Short text auto scoring requires at least one acceptable answer.");
        }

        return new ShortTextBody
        {
            AcceptableAnswers = answers,
            CaseSensitive = parsed.CaseSensitive
        };
    }

    private static LongTextBody NormalizeLongText(JsonElement body, ScoringMode scoring)
    {
        if (scoring == ScoringMode.Auto)
        {
            throw new DomainException("Long text questions cannot use auto scoring.");
        }

        var parsed = Deserialize<LongTextBody>(body);
        if (parsed.MaxLength is { } max && max < 1)
        {
            throw new DomainException("Long text maxLength must be at least 1 when set.");
        }

        var guidance = string.IsNullOrWhiteSpace(parsed.Guidance) ? null : parsed.Guidance.Trim();
        if (guidance is { Length: > Question.StemMaxLength })
        {
            throw new DomainException($"Guidance cannot exceed {Question.StemMaxLength} characters.");
        }

        return new LongTextBody { MaxLength = parsed.MaxLength, Guidance = guidance };
    }

    private static DragDropSharedBankBody NormalizeDragDropSharedBank(JsonElement body, ScoringMode scoring)
    {
        var parsed = Deserialize<DragDropSharedBankBody>(body);
        if (parsed.Slots is null || parsed.Slots.Count == 0)
        {
            throw new DomainException("Shared-bank drag-and-drop requires at least one slot.");
        }

        if (parsed.Bank is null || parsed.Bank.Count == 0)
        {
            throw new DomainException("Shared-bank drag-and-drop requires a non-empty bank.");
        }

        var bank = parsed.Bank.Select(item =>
        {
            var id = RequireId(item.Id, "Bank item");
            var text = RequireText(item.Text, "Bank item text");
            return new BankItem { Id = id, Text = text, IsDistractor = item.IsDistractor };
        }).ToList();
        EnsureUniqueIds(bank.Select(b => b.Id), "bank item");

        var nonDistractors = bank.Where(b => !b.IsDistractor).Select(b => b.Id)
            .ToHashSet(StringComparer.Ordinal);
        if (nonDistractors.Count == 0)
        {
            throw new DomainException("Shared-bank drag-and-drop requires at least one non-distractor bank item.");
        }

        var slots = parsed.Slots.Select(slot =>
        {
            var id = RequireId(slot.Id, "Slot");
            var label = RequireText(slot.Label, "Slot label");
            var correctItemId = RequireId(slot.CorrectItemId, "Slot correctItemId");
            if (scoring == ScoringMode.Auto && !nonDistractors.Contains(correctItemId))
            {
                throw new DomainException(
                    $"Slot '{id}' correctItemId must reference a non-distractor bank item.");
            }

            if (scoring != ScoringMode.Auto
                && bank.All(b => !string.Equals(b.Id, correctItemId, StringComparison.Ordinal)))
            {
                throw new DomainException($"Slot '{id}' correctItemId must reference a bank item.");
            }

            return new SharedBankSlot { Id = id, Label = label, CorrectItemId = correctItemId };
        }).ToList();
        EnsureUniqueIds(slots.Select(s => s.Id), "slot");

        return new DragDropSharedBankBody { Slots = slots, Bank = bank };
    }

    private static DragDropPerSlotBody NormalizeDragDropPerSlot(JsonElement body, ScoringMode scoring)
    {
        var parsed = Deserialize<DragDropPerSlotBody>(body);
        if (parsed.Slots is null || parsed.Slots.Count == 0)
        {
            throw new DomainException("Per-slot drag-and-drop requires at least one slot.");
        }

        var slots = parsed.Slots.Select(slot =>
        {
            var id = RequireId(slot.Id, "Slot");
            var label = RequireText(slot.Label, "Slot label");
            if (slot.Options is null || slot.Options.Count < 2)
            {
                throw new DomainException($"Slot '{id}' requires at least two options.");
            }

            var options = slot.Options.Select(NormalizeChoice).ToList();
            EnsureUniqueIds(options.Select(o => o.Id), $"slot '{id}' option");

            var correctCount = options.Count(o => o.IsCorrect);
            if (scoring == ScoringMode.Auto && correctCount != 1)
            {
                throw new DomainException($"Slot '{id}' auto scoring requires exactly one correct option.");
            }

            return new PerSlot { Id = id, Label = label, Options = options };
        }).ToList();
        EnsureUniqueIds(slots.Select(s => s.Id), "slot");

        return new DragDropPerSlotBody { Slots = slots };
    }

    private static OrderingBody NormalizeOrdering(JsonElement body)
    {
        var parsed = Deserialize<OrderingBody>(body);
        if (parsed.Items is null || parsed.Items.Count < 2)
        {
            throw new DomainException("Ordering questions require at least two items.");
        }

        var items = parsed.Items.Select(item =>
        {
            var id = RequireId(item.Id, "Ordering item");
            var text = RequireText(item.Text, "Ordering item text");
            return new OrderingItem { Id = id, Text = text, CorrectIndex = item.CorrectIndex };
        }).ToList();
        EnsureUniqueIds(items.Select(i => i.Id), "ordering item");

        var indexes = items.Select(i => i.CorrectIndex).OrderBy(i => i).ToList();
        var expected = Enumerable.Range(0, items.Count);
        if (!indexes.SequenceEqual(expected))
        {
            throw new DomainException(
                "Ordering correctIndex values must be unique and cover 0..n-1.");
        }

        return new OrderingBody { Items = items };
    }

    private static ChoiceOption NormalizeChoice(ChoiceOption option)
    {
        return new ChoiceOption
        {
            Id = RequireId(option.Id, "Option"),
            Text = RequireText(option.Text, "Option text"),
            IsCorrect = option.IsCorrect
        };
    }

    private static bool HasKeyedShortAnswers(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object
            || !body.TryGetProperty("acceptableAnswers", out var answers)
            || answers.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return answers.EnumerateArray().Any(item =>
            item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()));
    }

    private static T Deserialize<T>(JsonElement body)
    {
        try
        {
            var value = JsonSerializer.Deserialize<T>(body, CatalogJson.SerializerOptions);
            if (value is null)
            {
                throw new DomainException("Question body is invalid.");
            }

            return value;
        }
        catch (JsonException ex)
        {
            throw new DomainException($"Question body is invalid: {ex.Message}");
        }
    }

    private static string RequireId(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{name} id is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > 64)
        {
            throw new DomainException($"{name} id cannot exceed 64 characters.");
        }

        return trimmed;
    }

    private static string RequireText(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{name} is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > Question.StemMaxLength)
        {
            throw new DomainException($"{name} cannot exceed {Question.StemMaxLength} characters.");
        }

        return trimmed;
    }

    private static void EnsureUniqueIds(IEnumerable<string> ids, string noun)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            if (!seen.Add(id))
            {
                throw new DomainException($"Duplicate {noun} id '{id}'.");
            }
        }
    }

    internal sealed class MultipleChoiceOptionsBody
    {
        public List<ChoiceOption> Options { get; set; } = [];
    }

    internal sealed class ChoiceOption
    {
        public string Id { get; set; } = "";
        public string Text { get; set; } = "";
        public bool IsCorrect { get; set; }
    }

    internal sealed class TrueFalseBody
    {
        public bool Correct { get; set; }
    }

    internal sealed class ShortTextBody
    {
        public List<string>? AcceptableAnswers { get; set; }
        public bool CaseSensitive { get; set; }
    }

    internal sealed class LongTextBody
    {
        public int? MaxLength { get; set; }
        public string? Guidance { get; set; }
    }

    internal sealed class DragDropSharedBankBody
    {
        public List<SharedBankSlot>? Slots { get; set; }
        public List<BankItem>? Bank { get; set; }
    }

    internal sealed class SharedBankSlot
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        public string CorrectItemId { get; set; } = "";
    }

    internal sealed class BankItem
    {
        public string Id { get; set; } = "";
        public string Text { get; set; } = "";
        public bool IsDistractor { get; set; }
    }

    internal sealed class DragDropPerSlotBody
    {
        public List<PerSlot>? Slots { get; set; }
    }

    internal sealed class PerSlot
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        public List<ChoiceOption>? Options { get; set; }
    }

    internal sealed class OrderingBody
    {
        public List<OrderingItem>? Items { get; set; }
    }

    internal sealed class OrderingItem
    {
        public string Id { get; set; } = "";
        public string Text { get; set; } = "";
        public int CorrectIndex { get; set; }
    }
}
