namespace InterviewQuiz.Access.Authentication;

public sealed class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    /// <summary>
    /// Email of the first Super Admin. Empty skips user bootstrap. The role is still seeded.
    /// Environment variable: Bootstrap__SuperAdminEmail. Do not commit a real address with a password.
    /// </summary>
    public string? SuperAdminEmail { get; set; }

    /// <summary>
    /// When true, a Super Admin who still must set a password receives a new one-time token on startup.
    /// Leave false after the operator has copied the token from the log.
    /// </summary>
    public bool ResetSuperAdminSetup { get; set; }
}
