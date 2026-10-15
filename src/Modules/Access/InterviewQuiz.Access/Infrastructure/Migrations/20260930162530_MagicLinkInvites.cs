using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewQuiz.Access.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MagicLinkInvites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "magic_link_invites",
                schema: "access",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_magic_link_invites", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_magic_link_invites_assignment_current",
                schema: "access",
                table: "magic_link_invites",
                column: "AssignmentId",
                unique: true,
                filter: "\"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_magic_link_invites_TokenHash",
                schema: "access",
                table: "magic_link_invites",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "magic_link_invites",
                schema: "access");
        }
    }
}
