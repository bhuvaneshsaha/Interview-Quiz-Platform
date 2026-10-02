namespace InterviewQuiz.Access.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumSigningKeyLength = 32;

    public string Issuer { get; set; } = "";

    public string Audience { get; set; } = "";

    /// <summary>
    /// Symmetric HMAC-SHA256 key. Set via Jwt__SigningKey. Never commit a production value.
    /// </summary>
    public string SigningKey { get; set; } = "";

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 14;

    /// <summary>
    /// Candidate access token lifetime. Set via Jwt__CandidateAccessTokenMinutes. Default 60, range 1–180.
    /// </summary>
    public int CandidateAccessTokenMinutes { get; set; } = 60;
}
