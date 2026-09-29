using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace InterviewQuiz.Access.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "access");

            migrationBuilder.CreateTable(
                name: "permissions",
                schema: "access",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Module = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IncludeInEmployeeRoleEditor = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.Code);
                });

            migrationBuilder.InsertData(
                schema: "access",
                table: "permissions",
                columns: new[] { "Code", "DisplayName", "IncludeInEmployeeRoleEditor", "Module" },
                values: new object[,]
                {
                    { "ai.draft.use", "Use AI draft", true, "catalog" },
                    { "ai.rules.manage", "Manage company AI rule sets", true, "catalog" },
                    { "archive.restore.attempts", "Restore archived attempts", true, "access" },
                    { "archive.restore.catalog", "Restore archived quiz content", true, "access" },
                    { "archive.restore.resumes", "Restore archived resumes", true, "access" },
                    { "assignments.read", "View assignments", true, "delivery" },
                    { "assignments.write", "Assign quizzes", true, "delivery" },
                    { "attempts.read", "View attempts and results", true, "evaluation" },
                    { "attempts.review", "Review attempts", true, "evaluation" },
                    { "candidate.attempt.participate", "Take and submit own attempt", false, "candidate" },
                    { "filters.share", "Share saved filters", true, "search" },
                    { "filters.write", "Manage own saved filters", true, "search" },
                    { "openings.fields.manage", "Manage opening field defaults", true, "openings" },
                    { "openings.read", "View openings", true, "openings" },
                    { "openings.write", "Create and edit openings", true, "openings" },
                    { "quizzes.read", "View quizzes", true, "catalog" },
                    { "quizzes.write", "Create and edit quizzes", true, "catalog" },
                    { "roles.manage", "Manage permission groups", true, "access" },
                    { "sessions.live.run", "Run live sessions", true, "delivery" },
                    { "templates.read", "View templates", true, "catalog" },
                    { "templates.write", "Create and edit templates", true, "catalog" },
                    { "users.manage", "Manage users", true, "access" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "permissions",
                schema: "access");
        }
    }
}
