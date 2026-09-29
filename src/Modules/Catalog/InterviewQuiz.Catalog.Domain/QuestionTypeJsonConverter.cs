using System.Text.Json;
using System.Text.Json.Serialization;

namespace InterviewQuiz.Catalog.Domain;

/// <summary>Reads camelCase type names and rejects code / coding question types.</summary>
public sealed class QuestionTypeJsonConverter : JsonConverter<QuestionType>
{
    public override QuestionType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Question type must be a string.");
        }

        var raw = reader.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new JsonException("Question type is required.");
        }

        if (IsCodeType(raw))
        {
            throw new JsonException("Code question types are not supported.");
        }

        foreach (var value in Enum.GetValues<QuestionType>())
        {
            var camel = JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
            if (string.Equals(camel, raw, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value.ToString(), raw, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        throw new JsonException($"Unknown question type '{raw}'.");
    }

    public override void Write(Utf8JsonWriter writer, QuestionType value, JsonSerializerOptions options)
        => writer.WriteStringValue(JsonNamingPolicy.CamelCase.ConvertName(value.ToString()));

    private static bool IsCodeType(string raw)
        => raw.Equals("code", StringComparison.OrdinalIgnoreCase)
           || raw.Equals("coding", StringComparison.OrdinalIgnoreCase)
           || raw.Contains("codeQuestion", StringComparison.OrdinalIgnoreCase)
           || raw.Contains("codingProblem", StringComparison.OrdinalIgnoreCase);
}
