using InterviewQuiz.Access.Domain;
using InterviewQuiz.Access.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace InterviewQuiz.Access.Application.Services;

public sealed class EffectivePermissionReader : IEffectivePermissionReader
{
    private readonly AccessDbContext _db;

    public EffectivePermissionReader(AccessDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<string>> GetAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        return await _db.UserRoles
            .AsNoTracking()
            .Where(assignment => assignment.UserId == userId)
            .SelectMany(assignment => assignment.Role.Permissions)
            .Select(permission => permission.PermissionCode)
            .Distinct()
            .OrderBy(code => code)
            .ToListAsync(cancellationToken);
    }
}
