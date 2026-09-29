using Microsoft.AspNetCore.Identity;

namespace InterviewQuiz.Access.Domain;

/// <summary>
/// ASP.NET Core Identity user store row. Authentication to the API is JWT bearer, not cookies.
/// </summary>
public sealed class ApplicationUser : IdentityUser
{
    public bool IsDisabled { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
