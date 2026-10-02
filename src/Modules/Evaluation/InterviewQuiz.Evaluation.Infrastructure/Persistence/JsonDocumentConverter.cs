using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace InterviewQuiz.Evaluation.Infrastructure.Persistence;

internal static class JsonDocumentConverter
{
    public static ValueConverter<JsonDocument, string> Converter { get; } = new(
        document => document.RootElement.GetRawText(),
        json => JsonDocument.Parse(json));

    public static ValueComparer<JsonDocument> Comparer { get; } = new(
        (left, right) => left != null && right != null
            && left.RootElement.GetRawText() == right.RootElement.GetRawText(),
        document => document.RootElement.GetRawText().GetHashCode(),
        document => JsonDocument.Parse(document.RootElement.GetRawText()));
}
