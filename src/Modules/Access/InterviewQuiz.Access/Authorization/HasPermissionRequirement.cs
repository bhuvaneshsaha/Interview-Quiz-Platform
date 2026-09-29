using Microsoft.AspNetCore.Authorization;

namespace InterviewQuiz.Access.Authorization;

public sealed class HasPermissionRequirement : IAuthorizationRequirement
{
    public HasPermissionRequirement(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        Permission = permission;
    }

    public string Permission { get; }
}
