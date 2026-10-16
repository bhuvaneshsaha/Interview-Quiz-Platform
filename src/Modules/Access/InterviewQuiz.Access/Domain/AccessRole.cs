namespace InterviewQuiz.Access.Domain;

/// <summary>
/// Operator-composed permission set. Name is display-only — never used for authorization.
/// </summary>
public sealed class AccessRole
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// System roles are seeded and locked. Authorization still uses permission codes, never this flag or the name.
    /// </summary>
    public bool IsSystem { get; set; }

    /// <summary>
    /// Built-in roles are seeded in every environment, cannot be deleted, and keep an editable permission set.
    /// Authorization still uses permission codes, never this flag or the name.
    /// </summary>
    public bool IsBuiltIn { get; set; }

    public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();

    public ICollection<UserRoleAssignment> UserRoles { get; set; } = new List<UserRoleAssignment>();
}
