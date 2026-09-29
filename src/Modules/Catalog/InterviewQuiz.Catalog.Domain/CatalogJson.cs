using System.Text.Json;
using System.Text.Json.Serialization;

namespace InterviewQuiz.Catalog.Domain;

public static class CatalogJson
{
    public static JsonSerializerOptions SerializerOptions { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new QuestionTypeJsonConverter());
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }

    public static string ToCamelCase(Enum value) => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
}
