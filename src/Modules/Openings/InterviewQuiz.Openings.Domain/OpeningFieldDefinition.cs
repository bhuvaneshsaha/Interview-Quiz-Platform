namespace InterviewQuiz.Openings.Domain;

public sealed class OpeningFieldDefinition
{
    public const int KeyMaxLength = 64;
    public const int DisplayNameMaxLength = 128;

    private OpeningFieldDefinition()
    {
    }

    public Guid Id { get; private set; }
    public string Key { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public int SortOrder { get; private set; }

    public static OpeningFieldDefinition Create(string key, string displayName, int sortOrder, Guid? id = null)
    {
        var definition = new OpeningFieldDefinition { Id = id ?? Guid.NewGuid() };
        definition.Apply(key, displayName, sortOrder);
        return definition;
    }

    public void Apply(string key, string displayName, int sortOrder)
    {
        Key = Kernel.Tags.Tag.NormalizeKey(key);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new Kernel.Exceptions.DomainException("Field display name is required.");
        }

        var trimmed = displayName.Trim();
        if (trimmed.Length > DisplayNameMaxLength)
        {
            throw new Kernel.Exceptions.DomainException($"Field display name cannot exceed {DisplayNameMaxLength} characters.");
        }

        if (sortOrder < 0)
        {
            throw new Kernel.Exceptions.DomainException("Field sort order cannot be negative.");
        }

        DisplayName = trimmed;
        SortOrder = sortOrder;
    }
}
