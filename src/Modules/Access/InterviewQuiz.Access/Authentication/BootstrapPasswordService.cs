using System.Security.Cryptography;
using System.Text;
using InterviewQuiz.Access.Domain;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace InterviewQuiz.Access.Authentication;

public interface IBootstrapPasswordService
{
    /// <summary>
    /// Stores a hashed one-time token on the user and returns the raw token. The caller logs it once.
    /// </summary>
    string IssueToken(ApplicationUser user);

    Task CompleteAsync(string email, string token, string newPassword, CancellationToken cancellationToken);
}

public sealed class BootstrapPasswordService : IBootstrapPasswordService
{
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(72);

    private readonly UserManager<ApplicationUser> _users;
    private readonly IClock _clock;
    private readonly ILogger<BootstrapPasswordService> _logger;

    public BootstrapPasswordService(
        UserManager<ApplicationUser> users,
        IClock clock,
        ILogger<BootstrapPasswordService> logger)
    {
        _users = users;
        _clock = clock;
        _logger = logger;
    }

    public string IssueToken(ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        user.MustSetPassword = true;
        user.BootstrapTokenHash = Hash(raw);
        user.BootstrapTokenExpiresAt = _clock.UtcNow.Add(TokenLifetime);
        return raw;
    }

    public async Task CompleteAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(token)
            || string.IsNullOrWhiteSpace(newPassword))
        {
            throw new DomainException(SuperAdminRole.PasswordSetupInvalidMessage);
        }

        var user = await _users.FindByEmailAsync(email.Trim());
        if (user is null
            || string.IsNullOrWhiteSpace(user.BootstrapTokenHash)
            || user.BootstrapTokenExpiresAt is null
            || user.BootstrapTokenExpiresAt <= _clock.UtcNow
            || !FixedEquals(user.BootstrapTokenHash, Hash(token.Trim())))
        {
            throw new DomainException(SuperAdminRole.PasswordSetupInvalidMessage);
        }

        if (await _users.HasPasswordAsync(user))
        {
            var removed = await _users.RemovePasswordAsync(user);
            if (!removed.Succeeded)
            {
                throw new DomainException(string.Join(" ", removed.Errors.Select(error => error.Description)));
            }
        }

        var added = await _users.AddPasswordAsync(user, newPassword);
        if (!added.Succeeded)
        {
            throw new DomainException(string.Join(" ", added.Errors.Select(error => error.Description)));
        }

        user.MustSetPassword = false;
        user.BootstrapTokenHash = null;
        user.BootstrapTokenExpiresAt = null;
        var updated = await _users.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            throw new DomainException(string.Join(" ", updated.Errors.Select(error => error.Description)));
        }

        _logger.LogInformation("Bootstrap password set for user {UserId}", user.Id);
    }

    internal static string Hash(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }

    private static bool FixedEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
