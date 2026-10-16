using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewQuiz.Access.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedBuiltInAdminRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBuiltIn",
                schema: "access",
                table: "roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                UPDATE access.roles AS role
                SET "Name" = CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM access.roles AS other
                        WHERE other."Id" <> role."Id"
                          AND other."Name" = 'Admin (custom)')
                    THEN 'Admin (custom) ' || left(role."Id"::text, 8)
                    ELSE 'Admin (custom)'
                END
                WHERE role."Id" <> 'aaaaaaaa-0a01-4c9a-9e11-0f8c2b6a2004'
                  AND lower(role."Name") = 'admin';

                INSERT INTO access.roles ("Id", "Name", "Description", "IsSystem", "IsBuiltIn")
                VALUES (
                    'aaaaaaaa-0a01-4c9a-9e11-0f8c2b6a2004',
                    'Admin',
                    'Built-in role. Runs day-to-day administration and starts with every employee permission. Permissions can be edited. It cannot assign or revoke Super Admin, and it cannot be deleted.',
                    false,
                    true)
                ON CONFLICT ("Id") DO UPDATE
                SET "Name" = EXCLUDED."Name",
                    "Description" = EXCLUDED."Description",
                    "IsSystem" = false,
                    "IsBuiltIn" = true;

                INSERT INTO access.user_roles ("UserId", "RoleId")
                SELECT assignment."UserId", 'aaaaaaaa-0a01-4c9a-9e11-0f8c2b6a2004'
                FROM access.user_roles AS assignment
                INNER JOIN access.roles AS role ON role."Id" = assignment."RoleId"
                WHERE role."Id" <> 'aaaaaaaa-0a01-4c9a-9e11-0f8c2b6a2004'
                  AND role."Name" = 'Dev Admin'
                ON CONFLICT ("UserId", "RoleId") DO NOTHING;

                DELETE FROM access.roles
                WHERE "Id" <> 'aaaaaaaa-0a01-4c9a-9e11-0f8c2b6a2004'
                  AND "Name" = 'Dev Admin';

                INSERT INTO access.role_permissions ("RoleId", "PermissionCode")
                SELECT 'aaaaaaaa-0a01-4c9a-9e11-0f8c2b6a2004', permission."Code"
                FROM access.permissions AS permission
                WHERE permission."Code" <> 'candidate.attempt.participate'
                ON CONFLICT ("RoleId", "PermissionCode") DO NOTHING;

                DELETE FROM access.role_permissions
                WHERE "RoleId" = 'aaaaaaaa-0a01-4c9a-9e11-0f8c2b6a2004'
                  AND "PermissionCode" = 'candidate.attempt.participate';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE access.roles
                SET "Name" = 'Dev Admin',
                    "Description" = 'Local sample bundle (not a product role).',
                    "IsSystem" = false
                WHERE "Id" = 'aaaaaaaa-0a01-4c9a-9e11-0f8c2b6a2004';
                """);

            migrationBuilder.DropColumn(
                name: "IsBuiltIn",
                schema: "access",
                table: "roles");
        }
    }
}
