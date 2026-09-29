namespace InterviewQuiz.Access.Domain;

public sealed class RolePermission
{
    public Guid RoleId { get; set; }

    public required string PermissionCode { get; set; }

    public AccessRole Role { get; set; } = null!;

    public Permission Permission { get; set; } = null!;
}
