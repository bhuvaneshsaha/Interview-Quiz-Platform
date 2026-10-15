namespace InterviewQuiz.Access.Domain;

/// <summary>
/// Stable system role. Callers check this id, never the display name.
/// </summary>
public static class SuperAdminRole
{
    public static readonly Guid Id = Guid.Parse("f8c1a0b2-6d4e-4a11-9c3e-1b7a5d0e0001");

    public const string Name = "Super Admin";

    public const string Description =
        "System role. Holds every employee permission except candidate attempt access. Cannot be renamed, deleted, or reduced.";

    public const string ImmutableMessage =
        "Super Admin is a system role. It cannot be renamed, deleted, or have its permissions changed.";

    public const string AssignForbiddenMessage =
        "Only a Super Admin can assign or revoke the Super Admin role.";

    public const string LastHolderUnassignMessage =
        "The last active Super Admin cannot be removed.";

    public const string LastHolderDeactivateMessage =
        "The last active Super Admin cannot be deactivated.";

    public const string ReservedNameMessage = "Super Admin is a reserved system role name.";

    public const string AssignedAction = "superAdmin.assigned";

    public const string RevokedAction = "superAdmin.revoked";

    public const string PasswordSetupRequiredMessage =
        "Set a password with the one-time setup token before signing in.";

    public const string PasswordSetupInvalidMessage =
        "The password setup token is invalid or has expired.";
}
