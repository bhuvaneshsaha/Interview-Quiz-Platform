using InterviewQuiz.Access.Infrastructure;
using InterviewQuiz.Kernel.Users;
using Microsoft.EntityFrameworkCore;

namespace InterviewQuiz.Access.Application.Services;

public sealed class AssignableUserLookup : IAssignableUserLookup
{
    private readonly AccessDbContext _db;

    public AssignableUserLookup(AccessDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AssignableUserDto>> SearchAsync(
        string? query,
        int limit,
        CancellationToken cancellationToken)
    {
        var take = Math.Clamp(limit, 1, 20);
        var users = _db.Users.AsNoTracking().Where(user => !user.IsDisabled);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            var lower = term.ToLowerInvariant();
            users = users.Where(user =>
                user.Id == term
                || (user.Email != null && user.Email.ToLower().Contains(lower))
                || (user.UserName != null && user.UserName.ToLower().Contains(lower)));
        }

        var rows = await users
            .OrderBy(user => user.Email)
            .ThenBy(user => user.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyDictionary<string, AssignableUserDto>> GetByIdsAsync(
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken)
    {
        var ids = userIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<string, AssignableUserDto>(StringComparer.Ordinal);
        }

        var rows = await _db.Users.AsNoTracking()
            .Where(user => ids.Contains(user.Id) && !user.IsDisabled)
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(user => user.Id, Map, StringComparer.Ordinal);
    }

    internal static AssignableUserDto Map(Domain.ApplicationUser user)
        => new(
            user.Id,
            AssignableUserNames.FromEmail(user.Email, user.UserName),
            user.Email ?? user.UserName ?? "");
}

internal static class AssignableUserNames
{
    public static string FromEmail(string? email, string? userName)
    {
        var source = string.IsNullOrWhiteSpace(email) ? userName ?? "" : email;
        var local = source.Split('@')[0].Replace('.', ' ').Replace('_', ' ').Replace('-', ' ').Trim();
        if (string.IsNullOrWhiteSpace(local))
        {
            return string.IsNullOrWhiteSpace(source) ? "User" : source;
        }

        var parts = local.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', parts.Select(TitleCase));
    }

    private static string TitleCase(string part)
        => part.Length == 1
            ? char.ToUpperInvariant(part[0]).ToString()
            : char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant();
}
