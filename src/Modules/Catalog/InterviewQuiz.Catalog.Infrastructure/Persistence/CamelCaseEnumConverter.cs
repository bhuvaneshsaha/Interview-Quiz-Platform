using System.Text.Json;
using InterviewQuiz.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace InterviewQuiz.Catalog.Infrastructure.Persistence;

internal static class CamelCaseEnumConverter
{
    public static ValueConverter<TEnum, string> For<TEnum>() where TEnum : struct, Enum
        => new(
            value => JsonNamingPolicy.CamelCase.ConvertName(value.ToString()),
            stored => Parse<TEnum>(stored));

    public static ValueConverter<TEnum?, string?> ForNullable<TEnum>() where TEnum : struct, Enum
        => new(
            value => value.HasValue ? JsonNamingPolicy.CamelCase.ConvertName(value.Value.ToString()) : null,
            stored => string.IsNullOrWhiteSpace(stored) ? null : Parse<TEnum>(stored));

    private static TEnum Parse<TEnum>(string value) where TEnum : struct, Enum
    {
        foreach (var item in Enum.GetValues<TEnum>())
        {
            var camel = JsonNamingPolicy.CamelCase.ConvertName(item.ToString());
            if (string.Equals(camel, value, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        throw new InvalidOperationException($"Unknown {typeof(TEnum).Name} value '{value}'.");
    }
}
