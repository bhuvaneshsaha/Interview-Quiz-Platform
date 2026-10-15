using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewQuiz.Access.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReportsReadPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "access",
                table: "permissions",
                columns: new[] { "Code", "DisplayName", "IncludeInEmployeeRoleEditor", "Module" },
                values: new object[] { "reports.read", "View reports", true, "evaluation" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "access",
                table: "permissions",
                keyColumn: "Code",
                keyValue: "reports.read");
        }
    }
}
