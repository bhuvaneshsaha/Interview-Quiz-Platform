using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewQuiz.Search.Migrations
{
    /// <inheritdoc />
    public partial class InitialSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "search");

            migrationBuilder.CreateTable(
                name: "filters",
                schema: "search",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Target = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Criteria = table.Column<string>(type: "jsonb", nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ShareMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SharedWithUserIds = table.Column<List<string>>(type: "text[]", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_filters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_filters_OwnerUserId",
                schema: "search",
                table: "filters",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_filters_Target",
                schema: "search",
                table: "filters",
                column: "Target");

            migrationBuilder.CreateIndex(
                name: "IX_filters_UpdatedAtUtc",
                schema: "search",
                table: "filters",
                column: "UpdatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "filters",
                schema: "search");
        }
    }
}
