using System.Text.Json;
using System.Text.Json.Serialization;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Openings.Application.Contracts;
using InterviewQuiz.Search.Domain;

namespace InterviewQuiz.Search.Application;

internal static class FilterCriteriaParser
{
    private static readonly JsonSerializerOptions StrictOptions = Create(unmappedDisallowed: true);
    private static readonly JsonSerializerOptions PersistOptions = Create(unmappedDisallowed: false);

    public static JsonDocument Canonicalize(FilterTarget target, JsonElement criteria)
    {
        if (criteria.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new DomainException("Criteria is required.");
        }

        if (criteria.ValueKind != JsonValueKind.Object)
        {
            throw new DomainException("Criteria must be a JSON object.");
        }

        try
        {
            object parsed = target switch
            {
                FilterTarget.Openings => Deserialize<OpeningListCriteria>(criteria, "openings"),
                FilterTarget.Quizzes => Deserialize<QuizListCriteria>(criteria, "quizzes"),
                FilterTarget.Templates => Deserialize<TemplateListCriteria>(criteria, "templates"),
                _ => throw new DomainException("Unknown filter target.")
            };

            return JsonSerializer.SerializeToDocument(parsed, PersistOptions);
        }
        catch (JsonException ex)
        {
            throw new DomainException($"Filter criteria does not match the {ToCamel(target)} shape.", ex);
        }
        catch (NotSupportedException ex)
        {
            throw new DomainException($"Filter criteria does not match the {ToCamel(target)} shape.", ex);
        }
    }

    private static T Deserialize<T>(JsonElement criteria, string targetName)
    {
        var parsed = criteria.Deserialize<T>(StrictOptions);
        if (parsed is null)
        {
            throw new DomainException($"Filter criteria does not match the {targetName} shape.");
        }

        return parsed;
    }

    private static string ToCamel(FilterTarget target)
        => JsonNamingPolicy.CamelCase.ConvertName(target.ToString());

    private static JsonSerializerOptions Create(bool unmappedDisallowed)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        if (unmappedDisallowed)
        {
            options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        }

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
