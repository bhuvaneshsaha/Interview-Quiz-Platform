using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewQuiz.Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "quizzes",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OpeningId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    ExpectedExperienceYears = table.Column<int>(type: "integer", nullable: false),
                    Tags = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quizzes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "questions",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Stem = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    ScoringMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreditMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    Body = table.Column<string>(type: "jsonb", nullable: false),
                    QuizId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_questions_quizzes_QuizId",
                        column: x => x.QuizId,
                        principalSchema: "catalog",
                        principalTable: "quizzes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_questions_QuizId_SortOrder",
                schema: "catalog",
                table: "questions",
                columns: new[] { "QuizId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_quizzes_OpeningId",
                schema: "catalog",
                table: "quizzes",
                column: "OpeningId");

            migrationBuilder.CreateIndex(
                name: "IX_quizzes_Tags",
                schema: "catalog",
                table: "quizzes",
                column: "Tags")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_quizzes_UpdatedAtUtc",
                schema: "catalog",
                table: "quizzes",
                column: "UpdatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "questions",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "quizzes",
                schema: "catalog");
        }
    }
}
