using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace InterviewQuiz.Access.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class QuestionsPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "access",
                table: "permissions",
                columns: new[] { "Code", "DisplayName", "IncludeInEmployeeRoleEditor", "Module" },
                values: new object[,]
                {
                    { "questions.read", "View question bank", true, "catalog" },
                    { "questions.write", "Create and edit question bank", true, "catalog" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "access",
                table: "permissions",
                keyColumn: "Code",
                keyValue: "questions.read");

            migrationBuilder.DeleteData(
                schema: "access",
                table: "permissions",
                keyColumn: "Code",
                keyValue: "questions.write");
        }
    }
}
