using InterviewQuiz.Access.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InterviewQuiz.Access.Infrastructure.Identity;

public sealed class IdentityUserDirectory : IIdentityUserDirectory
{
    private readonly UserManager<ApplicationUser> _users;

    public IdentityUserDirectory(UserManager<ApplicationUser> users)
    {
        _users = users;
    }

    public Task<ApplicationUser?> FindByEmailAsync(string email, CancellationToken cancellationToken)
        => _users.FindByEmailAsync(email);

    public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
        => _users.FindByIdAsync(userId);

    public Task<bool> CheckPasswordAsync(ApplicationUser user, string password)
        => _users.CheckPasswordAsync(user, password);

    public Task<bool> IsLockedOutAsync(ApplicationUser user)
        => _users.IsLockedOutAsync(user);

    public async Task AccessFailedAsync(ApplicationUser user)
    {
        var result = await _users.AccessFailedAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(" ", result.Errors.Select(error => error.Description)));
        }
    }

    public async Task ResetAccessFailedCountAsync(ApplicationUser user)
    {
        var result = await _users.ResetAccessFailedCountAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(" ", result.Errors.Select(error => error.Description)));
        }
    }

    public async Task<IdentityCreateResult> CreateAsync(ApplicationUser user, string password)
    {
        var result = await _users.CreateAsync(user, password);
        return result.Succeeded
            ? IdentityCreateResult.Ok()
            : IdentityCreateResult.Fail(result.Errors.Select(error => error.Description));
    }

    public async Task<IdentityCreateResult> CreateWithoutPasswordAsync(ApplicationUser user)
    {
        var result = await _users.CreateAsync(user);
        return result.Succeeded
            ? IdentityCreateResult.Ok()
            : IdentityCreateResult.Fail(result.Errors.Select(error => error.Description));
    }

    public async Task UpdateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var result = await _users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(" ", result.Errors.Select(error => error.Description)));
        }
    }

    public Task<int> CountAsync(CancellationToken cancellationToken)
        => _users.Users.CountAsync(cancellationToken);

    public async Task<IReadOnlyList<ApplicationUser>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        return await _users.Users
            .OrderBy(user => user.Email)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}
