using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewQuiz.Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_drafts",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OpeningId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExpectedExperienceYears = table.Column<int>(type: "integer", nullable: false),
                    ResumeText = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    RulesText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplateVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    generated_questions_json = table.Column<string>(type: "text", nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedQuizId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcceptedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_drafts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_drafts_CreatedByUserId",
                schema: "catalog",
                table: "ai_drafts",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_drafts_OpeningId",
                schema: "catalog",
                table: "ai_drafts",
                column: "OpeningId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_drafts",
                schema: "catalog");
        }
    }
}
