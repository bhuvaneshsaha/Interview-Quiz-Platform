namespace InterviewQuiz.Access.Domain;

public sealed class UserRoleAssignment
{
    public string UserId { get; set; } = null!;

    public Guid RoleId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public AccessRole Role { get; set; } = null!;
}
