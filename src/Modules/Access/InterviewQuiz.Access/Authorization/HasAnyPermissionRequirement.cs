using Microsoft.AspNetCore.Authorization;

namespace InterviewQuiz.Access.Authorization;

/// <summary>
/// Succeeds when the principal has any of the listed permission codes (OR).
/// Still permission-based — never a role-name check.
/// </summary>
public sealed class HasAnyPermissionRequirement : IAuthorizationRequirement
{
    public HasAnyPermissionRequirement(IReadOnlyList<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        if (permissions.Count == 0 || permissions.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("At least one permission code is required.", nameof(permissions));
        }

        Permissions = permissions;
    }

    public IReadOnlyList<string> Permissions { get; }
}
