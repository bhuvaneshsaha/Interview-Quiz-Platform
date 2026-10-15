using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewQuiz.Access.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SuperAdminAuditAndBootstrap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSystem",
                schema: "access",
                table: "roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "BootstrapTokenExpiresAt",
                schema: "access",
                table: "identity_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BootstrapTokenHash",
                schema: "access",
                table: "identity_users",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MustSetPassword",
                schema: "access",
                table: "identity_users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "audit_events",
                schema: "access",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TargetUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_Action_TargetUserId",
                schema: "access",
                table: "audit_events",
                columns: new[] { "Action", "TargetUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_OccurredAtUtc",
                schema: "access",
                table: "audit_events",
                column: "OccurredAtUtc");

            migrationBuilder.Sql(
                """
                INSERT INTO access.roles ("Id", "Name", "Description", "IsSystem")
                VALUES (
                    'f8c1a0b2-6d4e-4a11-9c3e-1b7a5d0e0001',
                    'Super Admin',
                    'System role. Holds every employee permission except candidate attempt access. Cannot be renamed, deleted, or reduced.',
                    true)
                ON CONFLICT ("Id") DO NOTHING;

                INSERT INTO access.role_permissions ("RoleId", "PermissionCode")
                SELECT 'f8c1a0b2-6d4e-4a11-9c3e-1b7a5d0e0001', "Code"
                FROM access.permissions
                WHERE "Code" <> 'candidate.attempt.participate'
                ON CONFLICT ("RoleId", "PermissionCode") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM access.user_roles WHERE "RoleId" = 'f8c1a0b2-6d4e-4a11-9c3e-1b7a5d0e0001';
                DELETE FROM access.role_permissions WHERE "RoleId" = 'f8c1a0b2-6d4e-4a11-9c3e-1b7a5d0e0001';
                DELETE FROM access.roles WHERE "Id" = 'f8c1a0b2-6d4e-4a11-9c3e-1b7a5d0e0001';
                """);

            migrationBuilder.DropTable(
                name: "audit_events",
                schema: "access");

            migrationBuilder.DropColumn(
                name: "IsSystem",
                schema: "access",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "BootstrapTokenExpiresAt",
                schema: "access",
                table: "identity_users");

            migrationBuilder.DropColumn(
                name: "BootstrapTokenHash",
                schema: "access",
                table: "identity_users");

            migrationBuilder.DropColumn(
                name: "MustSetPassword",
                schema: "access",
                table: "identity_users");
        }
    }
}
