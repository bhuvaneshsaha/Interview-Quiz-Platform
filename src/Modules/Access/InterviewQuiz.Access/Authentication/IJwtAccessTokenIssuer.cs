namespace InterviewQuiz.Access.Authentication;

public sealed record IssuedAccessToken(string Token, DateTimeOffset ExpiresAt);

public interface IJwtAccessTokenIssuer
{
    IssuedAccessToken Issue(string userId, string email, IEnumerable<string> permissions);

    IssuedAccessToken IssueCandidate(string userId, string email, Guid assignmentId, Guid? attemptId);
}
