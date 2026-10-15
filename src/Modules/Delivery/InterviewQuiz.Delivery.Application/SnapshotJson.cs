using System.Text.Json;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;

namespace InterviewQuiz.Delivery.Application;

public static class SnapshotJson
{
    public static JsonDocument FromDto(QuizSnapshotDto snapshot)
    {
        var json = JsonSerializer.Serialize(snapshot, CatalogJson.SerializerOptions);
        return JsonDocument.Parse(json);
    }

    public static QuizSnapshotDto ToDto(JsonDocument payload)
    {
        var dto = JsonSerializer.Deserialize<QuizSnapshotDto>(payload.RootElement, CatalogJson.SerializerOptions);
        if (dto is null)
        {
            throw new InvalidOperationException("Assignment snapshot payload is invalid.");
        }

        return dto;
    }
}
