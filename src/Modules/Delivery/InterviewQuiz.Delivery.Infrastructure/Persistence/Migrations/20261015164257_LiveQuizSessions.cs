using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewQuiz.Delivery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LiveQuizSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CandidateLastSeenAtUtc",
                schema: "delivery",
                table: "assignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentQuestionIndex",
                schema: "delivery",
                table: "assignments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastActivityAtUtc",
                schema: "delivery",
                table: "assignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LiveEndedAtUtc",
                schema: "delivery",
                table: "assignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LiveStartedAtUtc",
                schema: "delivery",
                table: "assignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LiveState",
                schema: "delivery",
                table: "assignments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PausedAtUtc",
                schema: "delivery",
                table: "assignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SessionEndReason",
                schema: "delivery",
                table: "assignments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalPausedSeconds",
                schema: "delivery",
                table: "assignments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "live_session_events",
                schema: "delivery",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_session_events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_live_session_events_AssignmentId",
                schema: "delivery",
                table: "live_session_events",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_live_session_events_AssignmentId_AtUtc",
                schema: "delivery",
                table: "live_session_events",
                columns: new[] { "AssignmentId", "AtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "live_session_events",
                schema: "delivery");

            migrationBuilder.DropColumn(
                name: "CandidateLastSeenAtUtc",
                schema: "delivery",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "CurrentQuestionIndex",
                schema: "delivery",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "LastActivityAtUtc",
                schema: "delivery",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "LiveEndedAtUtc",
                schema: "delivery",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "LiveStartedAtUtc",
                schema: "delivery",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "LiveState",
                schema: "delivery",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "PausedAtUtc",
                schema: "delivery",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "SessionEndReason",
                schema: "delivery",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "TotalPausedSeconds",
                schema: "delivery",
                table: "assignments");
        }
    }
}
