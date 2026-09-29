using InterviewQuiz.Kernel.Exceptions;

namespace InterviewQuiz.Kernel.Tags;

public sealed record Tag
{
    public const int KeyMaxLength = 64;
    public const int ValueMaxLength = 256;

    public string Key { get; }
    public string Value { get; }

    public Tag(string key, string value)
    {
        Key = NormalizeKey(key);
        Value = NormalizeValue(value);
    }

    public static string NormalizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new DomainException("Tag key is required.");
        }

        var trimmed = key.Trim();
        if (trimmed.Length > KeyMaxLength)
        {
            throw new DomainException($"Tag key cannot exceed {KeyMaxLength} characters.");
        }

        foreach (var ch in trimmed)
        {
            if (!char.IsLetterOrDigit(ch) && ch is not '.' and not '_' and not '-')
            {
                throw new DomainException("Tag keys may contain letters, digits, '.', '_' and '-' only.");
            }
        }

        return trimmed;
    }

    public static string NormalizeValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Tag value is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > ValueMaxLength)
        {
            throw new DomainException($"Tag value cannot exceed {ValueMaxLength} characters.");
        }

        return trimmed;
    }
}
