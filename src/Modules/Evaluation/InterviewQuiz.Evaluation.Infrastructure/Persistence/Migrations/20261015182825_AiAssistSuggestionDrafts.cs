using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewQuiz.Evaluation.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiAssistSuggestionDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SuggestedAtUtc",
                schema: "evaluation",
                table: "attempt_item_results",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuggestedByUserId",
                schema: "evaluation",
                table: "attempt_item_results",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SuggestedPoints",
                schema: "evaluation",
                table: "attempt_item_results",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuggestionFeedback",
                schema: "evaluation",
                table: "attempt_item_results",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuggestionModel",
                schema: "evaluation",
                table: "attempt_item_results",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SuggestedAtUtc",
                schema: "evaluation",
                table: "attempt_item_results");

            migrationBuilder.DropColumn(
                name: "SuggestedByUserId",
                schema: "evaluation",
                table: "attempt_item_results");

            migrationBuilder.DropColumn(
                name: "SuggestedPoints",
                schema: "evaluation",
                table: "attempt_item_results");

            migrationBuilder.DropColumn(
                name: "SuggestionFeedback",
                schema: "evaluation",
                table: "attempt_item_results");

            migrationBuilder.DropColumn(
                name: "SuggestionModel",
                schema: "evaluation",
                table: "attempt_item_results");
        }
    }
}
