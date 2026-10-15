using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewQuiz.Access.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InviteExpiryAndProtectedToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAt",
                schema: "access",
                table: "magic_link_invites",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProtectedToken",
                schema: "access",
                table: "magic_link_invites",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                schema: "access",
                table: "magic_link_invites");

            migrationBuilder.DropColumn(
                name: "ProtectedToken",
                schema: "access",
                table: "magic_link_invites");
        }
    }
}
