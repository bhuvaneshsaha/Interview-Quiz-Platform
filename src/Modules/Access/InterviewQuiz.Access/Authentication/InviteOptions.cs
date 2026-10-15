namespace InterviewQuiz.Access.Authentication;

public sealed class InviteOptions
{
    public const string SectionName = "Invite";

    /// <summary>
    /// Hours added when a recruiter omits <c>expiresAt</c>.
    /// <c>0</c> leaves the invite without a time limit until someone sets one.
    /// </summary>
    public int DefaultLifetimeHours { get; set; } = 168;

    public const int MaxLifetimeHours = 24 * 366;
}
