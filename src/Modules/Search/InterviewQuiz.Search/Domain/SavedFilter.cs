using System.Text.Json;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;

namespace InterviewQuiz.Search.Domain;

public sealed class SavedFilter
{
    public const int NameMaxLength = 200;
    public const int OwnerUserIdMaxLength = 256;

    private SavedFilter()
    {
        Criteria = JsonDocument.Parse("{}");
        SharedWithUserIds = [];
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public FilterTarget Target { get; private set; }
    public JsonDocument Criteria { get; private set; }
    public string OwnerUserId { get; private set; } = null!;
    public FilterShareMode ShareMode { get; private set; }
    public List<string> SharedWithUserIds { get; private set; } = [];
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static SavedFilter Create(
        string ownerUserId,
        string name,
        FilterTarget target,
        JsonDocument criteria,
        IClock clock,
        Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(ownerUserId))
        {
            throw new DomainException("Owner is required.");
        }

        var now = clock.UtcNow;
        var filter = new SavedFilter
        {
            Id = id ?? Guid.NewGuid(),
            OwnerUserId = RequireOwner(ownerUserId),
            ShareMode = FilterShareMode.Private,
            SharedWithUserIds = [],
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        filter.ApplyDefinition(name, target, criteria, now);
        return filter;
    }

    public void Update(string name, FilterTarget target, JsonDocument criteria, IClock clock)
        => ApplyDefinition(name, target, criteria, clock.UtcNow);

    public void Share(FilterShareMode shareMode, IReadOnlyList<string>? userIds, IClock clock)
    {
        if (shareMode is not (FilterShareMode.PublicInsideCompany or FilterShareMode.SpecificUsers))
        {
            throw new DomainException("Share mode must be publicInsideCompany or specificUsers.");
        }

        if (shareMode == FilterShareMode.SpecificUsers)
        {
            var normalized = NormalizeUserIds(userIds);
            if (normalized.Count == 0)
            {
                throw new DomainException("User ids are required when sharing with specific users.");
            }

            ShareMode = FilterShareMode.SpecificUsers;
            SharedWithUserIds = normalized;
        }
        else
        {
            ShareMode = FilterShareMode.PublicInsideCompany;
            SharedWithUserIds = [];
        }

        UpdatedAtUtc = clock.UtcNow;
    }

    public void Unshare(IClock clock)
    {
        ShareMode = FilterShareMode.Private;
        SharedWithUserIds = [];
        UpdatedAtUtc = clock.UtcNow;
    }

    public bool IsOwnedBy(string userId)
        => string.Equals(OwnerUserId, userId, StringComparison.Ordinal);

    public bool IsVisibleTo(string userId)
    {
        if (IsOwnedBy(userId))
        {
            return true;
        }

        return ShareMode switch
        {
            FilterShareMode.PublicInsideCompany => true,
            FilterShareMode.SpecificUsers => SharedWithUserIds.Contains(userId, StringComparer.Ordinal),
            _ => false
        };
    }

    private void ApplyDefinition(string name, FilterTarget target, JsonDocument criteria, DateTimeOffset now)
    {
        if (!Enum.IsDefined(target))
        {
            throw new DomainException("Unknown filter target.");
        }

        if (criteria.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new DomainException("Criteria must be a JSON object.");
        }

        Name = RequireText(name, "Name", NameMaxLength);
        Target = target;
        Criteria = JsonDocument.Parse(criteria.RootElement.GetRawText());
        UpdatedAtUtc = now;
    }

    private static string RequireOwner(string ownerUserId)
    {
        var trimmed = ownerUserId.Trim();
        if (trimmed.Length > OwnerUserIdMaxLength)
        {
            throw new DomainException($"Owner cannot exceed {OwnerUserIdMaxLength} characters.");
        }

        return trimmed;
    }

    private static string RequireText(string value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{name} is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{name} cannot exceed {maxLength} characters.");
        }

        return trimmed;
    }

    private static List<string> NormalizeUserIds(IReadOnlyList<string>? userIds)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in userIds ?? [])
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var trimmed = raw.Trim();
            if (trimmed.Length > OwnerUserIdMaxLength)
            {
                throw new DomainException($"User id cannot exceed {OwnerUserIdMaxLength} characters.");
            }

            if (seen.Add(trimmed))
            {
                result.Add(trimmed);
            }
        }

        return result;
    }
}
