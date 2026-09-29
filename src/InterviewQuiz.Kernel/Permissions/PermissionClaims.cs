namespace InterviewQuiz.Kernel.Permissions;

/// <summary>
/// JWT / principal claim type for flattened permission codes.
/// Authorization checks this claim, never role names.
/// </summary>
public static class PermissionClaims
{
    public const string Permission = "permission";
}
