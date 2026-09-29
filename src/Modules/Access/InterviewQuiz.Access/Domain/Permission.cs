namespace InterviewQuiz.Access.Domain;

public sealed class Permission
{
    public required string Code { get; init; }
    public required string DisplayName { get; init; }
    public required string Module { get; init; }
    public bool IncludeInEmployeeRoleEditor { get; init; } = true;
}
