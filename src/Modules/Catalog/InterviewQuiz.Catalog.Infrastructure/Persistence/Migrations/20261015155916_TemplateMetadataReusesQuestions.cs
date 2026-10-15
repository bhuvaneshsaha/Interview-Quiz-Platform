using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewQuiz.Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TemplateMetadataReusesQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ContentVersionId",
                schema: "catalog",
                table: "template_versions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_template_versions_ContentVersionId",
                schema: "catalog",
                table: "template_versions",
                column: "ContentVersionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_template_versions_ContentVersionId",
                schema: "catalog",
                table: "template_versions");

            migrationBuilder.DropColumn(
                name: "ContentVersionId",
                schema: "catalog",
                table: "template_versions");
        }
    }
}
