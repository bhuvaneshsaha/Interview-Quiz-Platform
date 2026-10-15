using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewQuiz.Evaluation.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttemptDisplayOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisplayOrderJson",
                schema: "evaluation",
                table: "attempts",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<int>(
                name: "DisplaySeed",
                schema: "evaluation",
                table: "attempts",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayOrderJson",
                schema: "evaluation",
                table: "attempts");

            migrationBuilder.DropColumn(
                name: "DisplaySeed",
                schema: "evaluation",
                table: "attempts");
        }
    }
}
