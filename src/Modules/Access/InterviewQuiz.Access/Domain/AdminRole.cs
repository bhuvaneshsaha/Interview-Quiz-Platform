namespace InterviewQuiz.Access.Domain;

/// <summary>
/// Stable built-in role. Callers check this id, never the display name.
/// The id matches the former Development "Dev Admin" seed so existing assignments stay on one row.
/// </summary>
public static class AdminRole
{
    public static readonly Guid Id = Guid.Parse("aaaaaaaa-0a01-4c9a-9e11-0f8c2b6a2004");

    public const string Name = "Admin";

    public const string LegacyName = "Dev Admin";

    public const string Description =
        "Built-in role. Runs day-to-day administration and starts with every employee permission. Permissions can be edited. It cannot assign or revoke Super Admin, and it cannot be deleted.";

    public const string DeleteForbiddenMessage =
        "Admin is a built-in role. It cannot be deleted.";

    public const string ReservedNameMessage = "Admin is a reserved built-in role name.";
}
