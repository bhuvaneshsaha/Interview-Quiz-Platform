using InterviewQuiz.Access.Domain;
using InterviewQuiz.Access.Infrastructure;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Permissions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InterviewQuiz.Access.Infrastructure.Seeding;

/// <summary>
/// Development/Testing dummy users and sample permission bundles. Never runs in Production.
/// </summary>
public sealed class DevelopmentAccessSeeder
{
    public static readonly Guid RecruiterRoleId = Guid.Parse("aaaaaaaa-0a01-4c9a-9e11-0f8c2b6a2001");
    public static readonly Guid TemplateAuthorRoleId = Guid.Parse("aaaaaaaa-0a01-4c9a-9e11-0f8c2b6a2002");
    public static readonly Guid ReviewerRoleId = Guid.Parse("aaaaaaaa-0a01-4c9a-9e11-0f8c2b6a2003");
    public static readonly Guid AdminRoleId = Guid.Parse("aaaaaaaa-0a01-4c9a-9e11-0f8c2b6a2004");

    public const string RecruiterEmail = "recruiter.dev@example.com";
    public const string RecruiterPassword = "Dev.Recruiter!1";
    public const string AuthorEmail = "author.dev@example.com";
    public const string AuthorPassword = "Dev.Author!1";
    public const string ReviewerEmail = "reviewer.dev@example.com";
    public const string ReviewerPassword = "Dev.Reviewer!1";
    public const string AdminEmail = "admin.dev@example.com";
    public const string AdminPassword = "Dev.Admin!1";

    private readonly AccessDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IClock _clock;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DevelopmentAccessSeeder> _logger;

    public DevelopmentAccessSeeder(
        AccessDbContext db,
        UserManager<ApplicationUser> users,
        IClock clock,
        IHostEnvironment environment,
        ILogger<DevelopmentAccessSeeder> logger)
    {
        _db = db;
        _users = users;
        _clock = clock;
        _environment = environment;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment() && !_environment.IsEnvironment("Testing"))
        {
            _logger.LogWarning("Skipped Access development seed in {Environment}", _environment.EnvironmentName);
            return;
        }

        await SeedRoleAsync(
            RecruiterRoleId,
            "Dev Recruiter",
            "Local sample bundle (not a product role).",
            [
                PermissionCodes.Openings.Read,
                PermissionCodes.Openings.Write,
                PermissionCodes.Catalog.QuizzesRead,
                PermissionCodes.Catalog.TemplatesRead,
                PermissionCodes.Delivery.AssignmentsRead,
                PermissionCodes.Delivery.AssignmentsWrite,
                PermissionCodes.Delivery.SessionsLiveRun,
                PermissionCodes.Evaluation.AttemptsRead,
                PermissionCodes.Search.FiltersWrite,
                PermissionCodes.Search.FiltersShare
            ],
            cancellationToken);

        await SeedRoleAsync(
            TemplateAuthorRoleId,
            "Dev Template author",
            "Local sample bundle (not a product role).",
            [
                PermissionCodes.Openings.Read,
                PermissionCodes.Catalog.QuizzesRead,
                PermissionCodes.Catalog.QuizzesWrite,
                PermissionCodes.Catalog.TemplatesRead,
                PermissionCodes.Catalog.TemplatesWrite,
                PermissionCodes.Catalog.AiDraftUse,
                PermissionCodes.Search.FiltersWrite
            ],
            cancellationToken);

        await SeedRoleAsync(
            ReviewerRoleId,
            "Dev Reviewer",
            "Local sample bundle (not a product role).",
            [
                PermissionCodes.Openings.Read,
                PermissionCodes.Delivery.AssignmentsRead,
                PermissionCodes.Evaluation.AttemptsRead,
                PermissionCodes.Evaluation.AttemptsReview
            ],
            cancellationToken);

        await SeedRoleAsync(
            AdminRoleId,
            "Dev Admin",
            "Local sample bundle (not a product role).",
            [
                PermissionCodes.Access.UsersManage,
                PermissionCodes.Access.RolesManage,
                PermissionCodes.Openings.FieldsManage,
                PermissionCodes.Catalog.AiRulesManage,
                PermissionCodes.Access.ArchiveRestoreResumes,
                PermissionCodes.Access.ArchiveRestoreAttempts,
                PermissionCodes.Access.ArchiveRestoreCatalog,
                PermissionCodes.Openings.Read,
                PermissionCodes.Catalog.QuizzesRead,
                PermissionCodes.Catalog.TemplatesRead
            ],
            cancellationToken);

        await SeedUserAsync(RecruiterEmail, RecruiterPassword, RecruiterRoleId, cancellationToken);
        await SeedUserAsync(AuthorEmail, AuthorPassword, TemplateAuthorRoleId, cancellationToken);
        await SeedUserAsync(ReviewerEmail, ReviewerPassword, ReviewerRoleId, cancellationToken);
        await SeedUserAsync(AdminEmail, AdminPassword, AdminRoleId, cancellationToken);
    }

    private async Task SeedRoleAsync(
        Guid id,
        string name,
        string description,
        IReadOnlyList<string> permissionCodes,
        CancellationToken cancellationToken)
    {
        var role = await _db.Roles
            .Include(item => item.Permissions)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (role is null)
        {
            role = new AccessRole
            {
                Id = id,
                Name = name,
                Description = description
            };
            _db.Roles.Add(role);
        }
        else
        {
            role.Name = name;
            role.Description = description;
            role.Permissions.Clear();
        }

        foreach (var code in permissionCodes)
        {
            role.Permissions.Add(new RolePermission { RoleId = id, PermissionCode = code });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedUserAsync(
        string email,
        string password,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        var user = await _users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                IsDisabled = false,
                CreatedAt = _clock.UtcNow
            };

            var created = await _users.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to seed development user {email}: {string.Join(" ", created.Errors.Select(e => e.Description))}");
            }
        }

        var assigned = await _db.UserRoles.AnyAsync(
            assignment => assignment.UserId == user.Id && assignment.RoleId == roleId,
            cancellationToken);
        if (!assigned)
        {
            _db.UserRoles.Add(new UserRoleAssignment { UserId = user.Id, RoleId = roleId });
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
