using Microsoft.AspNetCore.Authorization;

namespace InterviewQuiz.Access.Authorization;

/// <summary>
/// Authorizes when the principal has any of the given permission codes. Never role names.
/// </summary>
public sealed class HasAnyPermissionAttribute : AuthorizeAttribute
{
    public HasAnyPermissionAttribute(params string[] permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        if (permissions.Length == 0 || permissions.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("At least one permission is required.", nameof(permissions));
        }

        Permissions = permissions;
        Policy = PermissionPolicyProvider.AnyPolicyPrefix + string.Join("|", permissions);
    }

    public IReadOnlyList<string> Permissions { get; }
}
