using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewQuiz.Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuizShuffleSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShuffleOptions",
                schema: "catalog",
                table: "quizzes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShuffleQuestions",
                schema: "catalog",
                table: "quizzes",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShuffleOptions",
                schema: "catalog",
                table: "quizzes");

            migrationBuilder.DropColumn(
                name: "ShuffleQuestions",
                schema: "catalog",
                table: "quizzes");
        }
    }
}
