using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewQuiz.Evaluation.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HumanReviewScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FinalPointsAwarded",
                schema: "evaluation",
                table: "attempts",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE evaluation.attempts
                SET "FinalPointsAwarded" = "AutoPointsAwarded"
                WHERE "ResultStatus" = 'complete';
                """);

            migrationBuilder.AddColumn<string>(
                name: "ReviewComment",
                schema: "evaluation",
                table: "attempt_item_results",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAtUtc",
                schema: "evaluation",
                table: "attempt_item_results",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByUserId",
                schema: "evaluation",
                table: "attempt_item_results",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinalPointsAwarded",
                schema: "evaluation",
                table: "attempts");

            migrationBuilder.DropColumn(
                name: "ReviewComment",
                schema: "evaluation",
                table: "attempt_item_results");

            migrationBuilder.DropColumn(
                name: "ReviewedAtUtc",
                schema: "evaluation",
                table: "attempt_item_results");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                schema: "evaluation",
                table: "attempt_item_results");
        }
    }
}
