using InterviewQuiz.Access.Domain;
using InterviewQuiz.Access.Infrastructure.Seeding;

namespace InterviewQuiz.Access.Infrastructure.Migrations;

/// <summary>
/// Grants <c>reports.read</c> to every employee role already stored.
/// A role that holds <c>candidate.attempt.participate</c> is left unchanged.
/// </summary>
public static class ReportsReadEmployeeGrant
{
    public static readonly string ApplySql =
        """
        INSERT INTO access.role_permissions ("RoleId", "PermissionCode")
        SELECT role."Id", 'reports.read'
        FROM access.roles AS role
        WHERE NOT EXISTS (
            SELECT 1
            FROM access.role_permissions AS held
            WHERE held."RoleId" = role."Id"
              AND held."PermissionCode" = 'reports.read')
          AND NOT EXISTS (
            SELECT 1
            FROM access.role_permissions AS candidate_role
            WHERE candidate_role."RoleId" = role."Id"
              AND candidate_role."PermissionCode" = 'candidate.attempt.participate');
        """;

    public static readonly string RevertSql =
        $"""
        DELETE FROM access.role_permissions AS grant_row
        WHERE grant_row."PermissionCode" = 'reports.read'
          AND grant_row."RoleId" NOT IN (
              '{SuperAdminRole.Id}',
              '{DevelopmentAccessSeeder.RecruiterRoleId}')
          AND NOT EXISTS (
            SELECT 1
            FROM access.role_permissions AS candidate_role
            WHERE candidate_role."RoleId" = grant_row."RoleId"
              AND candidate_role."PermissionCode" = 'candidate.attempt.participate');
        """;
}
