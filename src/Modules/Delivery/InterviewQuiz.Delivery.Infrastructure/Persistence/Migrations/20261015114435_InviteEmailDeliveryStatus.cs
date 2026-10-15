using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewQuiz.Delivery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InviteEmailDeliveryStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InviteEmailAttemptCount",
                schema: "delivery",
                table: "assignments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InviteEmailLastAttemptAt",
                schema: "delivery",
                table: "assignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InviteEmailLastError",
                schema: "delivery",
                table: "assignments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InviteEmailStatus",
                schema: "delivery",
                table: "assignments",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "none");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InviteEmailAttemptCount",
                schema: "delivery",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "InviteEmailLastAttemptAt",
                schema: "delivery",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "InviteEmailLastError",
                schema: "delivery",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "InviteEmailStatus",
                schema: "delivery",
                table: "assignments");
        }
    }
}
