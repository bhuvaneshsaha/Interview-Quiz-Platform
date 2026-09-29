using Microsoft.AspNetCore.Authorization;

namespace InterviewQuiz.Access.Authorization;

/// <summary>
/// Authorizes by permission code, never by role name.
/// Until Auth wires claims, <see cref="TemporaryAllowAuthenticatedPermissionHandler"/>
/// succeeds for any authenticated identity and fails closed for anonymous.
/// </summary>
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        Permission = permission;
        Policy = PermissionPolicyProvider.PolicyPrefix + permission;
    }

    public string Permission { get; }
}
