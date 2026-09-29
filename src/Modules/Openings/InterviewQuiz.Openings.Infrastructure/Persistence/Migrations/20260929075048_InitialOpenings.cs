using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewQuiz.Openings.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialOpenings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "openings");

            migrationBuilder.CreateTable(
                name: "opening_field_definitions",
                schema: "openings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_opening_field_definitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "openings",
                schema: "openings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    JobDescription = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: false),
                    Owner = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpectedCloseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Headcount = table.Column<int>(type: "integer", nullable: false),
                    ExpectedExperienceYears = table.Column<int>(type: "integer", nullable: false),
                    Handlers = table.Column<string>(type: "jsonb", nullable: false),
                    Tags = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_openings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_opening_field_definitions_Key",
                schema: "openings",
                table: "opening_field_definitions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_openings_ExpectedCloseDate",
                schema: "openings",
                table: "openings",
                column: "ExpectedCloseDate");

            migrationBuilder.CreateIndex(
                name: "IX_openings_ExpectedExperienceYears",
                schema: "openings",
                table: "openings",
                column: "ExpectedExperienceYears");

            migrationBuilder.CreateIndex(
                name: "IX_openings_Owner",
                schema: "openings",
                table: "openings",
                column: "Owner");

            migrationBuilder.CreateIndex(
                name: "IX_openings_StartDate",
                schema: "openings",
                table: "openings",
                column: "StartDate");

            migrationBuilder.CreateIndex(
                name: "IX_openings_Tags",
                schema: "openings",
                table: "openings",
                column: "Tags")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "opening_field_definitions",
                schema: "openings");

            migrationBuilder.DropTable(
                name: "openings",
                schema: "openings");
        }
    }
}
