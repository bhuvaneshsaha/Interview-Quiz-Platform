namespace InterviewQuiz.Kernel.Permissions;

/// <summary>
/// JWT / principal claim types. Authorization checks permission codes, never role names.
/// Candidate tokens also carry assignment (and attempt when started) resource scope.
/// </summary>
public static class PermissionClaims
{
    public const string Permission = "permission";

    public const string AssignmentId = "assignment_id";

    public const string AttemptId = "attempt_id";
}
