using InterviewQuiz.Access.Authentication;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Permissions;
using Microsoft.Extensions.Options;

namespace InterviewQuiz.Host.IntegrationTests;

internal static class JwtTestTokens
{
    public const string SigningKey = "InterviewQuiz-Testing-Signing-Key-Not-For-Production!";
    public const string Issuer = "InterviewQuiz.Tests";
    public const string Audience = "InterviewQuiz.Tests";

    public static string Recruiter()
        => Create(
            PermissionCodes.Openings.Read,
            PermissionCodes.Openings.Write,
            PermissionCodes.Openings.FieldsManage);

    public static string ReaderOnly()
        => Create(PermissionCodes.Openings.Read);

    public static string Create(params string[] permissions)
        => CreateForUser("integration-user", permissions);

    public static string CreateForUser(string userId, params string[] permissions)
        => Issue(UtcNow(), "InterviewQuiz.Tests", userId, permissions).Token;

    public static string CreateExpired(params string[] permissions)
        => Issue(DateTimeOffset.UtcNow.AddMinutes(-20), "InterviewQuiz.Tests", "integration-user", permissions).Token;

    public static string CreateWrongAudience(params string[] permissions)
        => Issue(UtcNow(), "not-the-api", "integration-user", permissions).Token;

    private static IssuedAccessToken Issue(
        DateTimeOffset now,
        string audience,
        string userId,
        IReadOnlyList<string> permissions)
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = Issuer,
            Audience = audience,
            SigningKey = SigningKey,
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7
        });
        var issuer = new JwtAccessTokenIssuer(options, new FixedClock(now));
        return issuer.Issue(userId, "tester@example.com", permissions);
    }

    private static DateTimeOffset UtcNow() => DateTimeOffset.UtcNow;

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow) => UtcNow = utcNow;

        public DateTimeOffset UtcNow { get; }
    }
}
