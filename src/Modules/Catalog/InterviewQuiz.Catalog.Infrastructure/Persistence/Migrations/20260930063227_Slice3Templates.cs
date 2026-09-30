using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewQuiz.Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Slice3Templates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OriginTemplateId",
                schema: "catalog",
                table: "quizzes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceTemplateVersionId",
                schema: "catalog",
                table: "quizzes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceQuestionId",
                schema: "catalog",
                table: "questions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "templates",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginQuizId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "template_versions",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    ExpectedExperienceYears = table.Column<int>(type: "integer", nullable: false),
                    Tags = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    PublishedFromQuizId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedByUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_template_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_template_versions_templates_TemplateId",
                        column: x => x.TemplateId,
                        principalSchema: "catalog",
                        principalTable: "templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "template_version_questions",
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
                    SourceQuestionId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplateVersionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_template_version_questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_template_version_questions_template_versions_TemplateVersio~",
                        column: x => x.TemplateVersionId,
                        principalSchema: "catalog",
                        principalTable: "template_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_quizzes_OriginTemplateId",
                schema: "catalog",
                table: "quizzes",
                column: "OriginTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_template_version_questions_TemplateVersionId_SortOrder",
                schema: "catalog",
                table: "template_version_questions",
                columns: new[] { "TemplateVersionId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_template_versions_PublishedAtUtc",
                schema: "catalog",
                table: "template_versions",
                column: "PublishedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_template_versions_Tags",
                schema: "catalog",
                table: "template_versions",
                column: "Tags")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_template_versions_TemplateId_VersionNumber",
                schema: "catalog",
                table: "template_versions",
                columns: new[] { "TemplateId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_templates_OriginQuizId",
                schema: "catalog",
                table: "templates",
                column: "OriginQuizId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_templates_UpdatedAtUtc",
                schema: "catalog",
                table: "templates",
                column: "UpdatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "template_version_questions",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "template_versions",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "templates",
                schema: "catalog");

            migrationBuilder.DropIndex(
                name: "IX_quizzes_OriginTemplateId",
                schema: "catalog",
                table: "quizzes");

            migrationBuilder.DropColumn(
                name: "OriginTemplateId",
                schema: "catalog",
                table: "quizzes");

            migrationBuilder.DropColumn(
                name: "SourceTemplateVersionId",
                schema: "catalog",
                table: "quizzes");

            migrationBuilder.DropColumn(
                name: "SourceQuestionId",
                schema: "catalog",
                table: "questions");
        }
    }
}
