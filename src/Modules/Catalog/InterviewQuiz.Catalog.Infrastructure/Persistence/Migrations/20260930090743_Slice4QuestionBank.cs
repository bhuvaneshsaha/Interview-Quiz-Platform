using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewQuiz.Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Slice4QuestionBank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bank_questions",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Tags = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    ExpectedExperienceYears = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Stem = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    ScoringMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreditMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    Body = table.Column<string>(type: "jsonb", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_questions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bank_questions_ArchivedAtUtc",
                schema: "catalog",
                table: "bank_questions",
                column: "ArchivedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_bank_questions_Tags",
                schema: "catalog",
                table: "bank_questions",
                column: "Tags")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_bank_questions_Type",
                schema: "catalog",
                table: "bank_questions",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_bank_questions_UpdatedAtUtc",
                schema: "catalog",
                table: "bank_questions",
                column: "UpdatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bank_questions",
                schema: "catalog");
        }
    }
}
