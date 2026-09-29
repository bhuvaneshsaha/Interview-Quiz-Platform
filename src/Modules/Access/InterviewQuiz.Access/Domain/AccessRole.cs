namespace InterviewQuiz.Access.Domain;

/// <summary>
/// Operator-composed permission set. Name is display-only — never used for authorization.
/// </summary>
public sealed class AccessRole
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();

    public ICollection<UserRoleAssignment> UserRoles { get; set; } = new List<UserRoleAssignment>();
}
