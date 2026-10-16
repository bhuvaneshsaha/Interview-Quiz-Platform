using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewQuiz.Access.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GrantReportsReadToEmployeeRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ReportsReadEmployeeGrant.ApplySql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ReportsReadEmployeeGrant.RevertSql);
        }
    }
}
