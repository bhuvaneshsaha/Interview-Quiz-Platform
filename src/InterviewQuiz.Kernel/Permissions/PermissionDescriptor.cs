namespace InterviewQuiz.Kernel.Permissions;

public sealed record PermissionDescriptor(
    string Code,
    string DisplayName,
    string Module,
    bool IncludeInEmployeeRoleEditor = true);
