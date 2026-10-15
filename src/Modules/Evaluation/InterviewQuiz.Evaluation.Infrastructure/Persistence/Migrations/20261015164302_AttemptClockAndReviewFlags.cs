using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewQuiz.Evaluation.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AttemptClockAndReviewFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PausedAtUtc",
                schema: "evaluation",
                table: "attempts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Flagged",
                schema: "evaluation",
                table: "attempt_answers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PausedAtUtc",
                schema: "evaluation",
                table: "attempts");

            migrationBuilder.DropColumn(
                name: "Flagged",
                schema: "evaluation",
                table: "attempt_answers");
        }
    }
}
