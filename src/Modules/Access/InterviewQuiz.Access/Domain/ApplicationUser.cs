using Microsoft.AspNetCore.Identity;

namespace InterviewQuiz.Access.Domain;

/// <summary>
/// ASP.NET Core Identity user store row. Authentication to the API is JWT bearer, not cookies.
/// </summary>
public sealed class ApplicationUser : IdentityUser
{
    public bool IsDisabled { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Bootstrap Super Admin has no password until the one-time setup token is consumed.
    /// </summary>
    public bool MustSetPassword { get; set; }

    public string? BootstrapTokenHash { get; set; }

    public DateTimeOffset? BootstrapTokenExpiresAt { get; set; }
}
