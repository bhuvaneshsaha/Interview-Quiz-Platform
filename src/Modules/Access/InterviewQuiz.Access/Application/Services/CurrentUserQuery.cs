using System.Security.Claims;
using InterviewQuiz.Access.Application.Contracts;
using InterviewQuiz.Access.Authentication;

namespace InterviewQuiz.Access.Application.Services;

public sealed class CurrentUserQuery : ICurrentUserQuery
{
    public MeResponse GetMe(ClaimsPrincipal principal)
    {
        var userId = principal.FindUserId()
            ?? throw new InvalidOperationException("Authenticated user is missing a subject claim.");
        var email = principal.FindEmail() ?? "";
        return new MeResponse(userId, email, principal.FindPermissions());
    }

    public IReadOnlyList<string> GetPermissions(ClaimsPrincipal principal)
        => principal.FindPermissions();
}
