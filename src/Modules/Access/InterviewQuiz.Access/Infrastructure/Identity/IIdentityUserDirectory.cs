using InterviewQuiz.Access.Domain;

namespace InterviewQuiz.Access.Infrastructure.Identity;

public interface IIdentityUserDirectory
{
    Task<ApplicationUser?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken);

    Task<bool> CheckPasswordAsync(ApplicationUser user, string password);

    Task<bool> IsLockedOutAsync(ApplicationUser user);

    Task AccessFailedAsync(ApplicationUser user);

    Task ResetAccessFailedCountAsync(ApplicationUser user);

    Task<IdentityCreateResult> CreateAsync(ApplicationUser user, string password);

    Task<IdentityCreateResult> CreateWithoutPasswordAsync(ApplicationUser user);

    Task UpdateAsync(ApplicationUser user, CancellationToken cancellationToken);

    Task<int> CountAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ApplicationUser>> ListAsync(int skip, int take, CancellationToken cancellationToken);
}

public sealed record IdentityCreateResult(bool Succeeded, IReadOnlyList<string> Errors)
{
    public static IdentityCreateResult Ok() => new(true, []);

    public static IdentityCreateResult Fail(IEnumerable<string> errors) => new(false, errors.ToArray());
}
