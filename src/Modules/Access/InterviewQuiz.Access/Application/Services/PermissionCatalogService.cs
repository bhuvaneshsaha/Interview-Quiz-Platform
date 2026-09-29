using InterviewQuiz.Access.Application.Contracts;
using InterviewQuiz.Access.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace InterviewQuiz.Access.Application.Services;

public sealed class PermissionCatalogService : IPermissionCatalogService
{
    private readonly AccessDbContext _db;

    public PermissionCatalogService(AccessDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PermissionResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await _db.Permissions
            .AsNoTracking()
            .OrderBy(permission => permission.Module)
            .ThenBy(permission => permission.Code)
            .ToListAsync(cancellationToken);

        return rows
            .Select(permission => new PermissionResponse(
                permission.Code,
                permission.DisplayName,
                permission.Module,
                permission.IncludeInEmployeeRoleEditor))
            .ToArray();
    }
}
