namespace InterviewQuiz.Access.Authentication;

/// <summary>
/// Origin used to build recruiter-copy invite URLs. Not a secret. Env name: PublicBaseUrl.
/// </summary>
public sealed class PublicBaseUrlOptions
{
    public const string ConfigurationKey = "PublicBaseUrl";

    public string PublicBaseUrl { get; set; } = "";
}
