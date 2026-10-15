using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewQuiz.Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TemplateEditVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "row_version",
                schema: "catalog",
                table: "templates",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<string>(
                name: "ChangeNote",
                schema: "catalog",
                table: "template_versions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "row_version",
                schema: "catalog",
                table: "templates");

            migrationBuilder.DropColumn(
                name: "ChangeNote",
                schema: "catalog",
                table: "template_versions");
        }
    }
}
