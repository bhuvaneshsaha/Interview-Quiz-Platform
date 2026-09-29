using InterviewQuiz.Access.Application.Contracts;

namespace InterviewQuiz.Access.Authentication;

public interface IAuthService
{
    Task<TokenResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<TokenResponse?> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken);

    Task LogoutAsync(RefreshRequest request, CancellationToken cancellationToken);
}
