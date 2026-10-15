namespace InterviewQuiz.Access.Authentication;

public enum MagicLinkLookupKind
{
    Unknown = 0,
    Active = 1,
    Expired = 2,
    Revoked = 3
}

public readonly record struct MagicLinkLookup(MagicLinkLookupKind Kind, Guid AssignmentId)
{
    public static MagicLinkLookup Unknown { get; } = new(MagicLinkLookupKind.Unknown, Guid.Empty);
}

public sealed record MagicLinkInviteStatus(string State, DateTimeOffset? ExpiresAt)
{
    public const string None = "none";
    public const string Active = "active";
    public const string Expired = "expired";
    public const string Revoked = "revoked";

    public static MagicLinkInviteStatus Missing { get; } = new(None, null);
}
