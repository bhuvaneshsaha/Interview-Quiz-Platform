using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewQuiz.Delivery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "delivery");

            migrationBuilder.CreateTable(
                name: "assignments",
                schema: "delivery",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OpeningId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuizId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OverallDurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    AttemptLimit = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "assignment_snapshots",
                schema: "delivery",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    QuestionCount = table.Column<int>(type: "integer", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assignment_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_assignment_snapshots_assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalSchema: "delivery",
                        principalTable: "assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_assignment_snapshots_AssignmentId",
                schema: "delivery",
                table: "assignment_snapshots",
                column: "AssignmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_assignment_snapshots_Title",
                schema: "delivery",
                table: "assignment_snapshots",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_assignments_CandidateEmail",
                schema: "delivery",
                table: "assignments",
                column: "CandidateEmail");

            migrationBuilder.CreateIndex(
                name: "IX_assignments_CreatedAtUtc",
                schema: "delivery",
                table: "assignments",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_assignments_OpeningId",
                schema: "delivery",
                table: "assignments",
                column: "OpeningId");

            migrationBuilder.CreateIndex(
                name: "IX_assignments_SnapshotId",
                schema: "delivery",
                table: "assignments",
                column: "SnapshotId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assignment_snapshots",
                schema: "delivery");

            migrationBuilder.DropTable(
                name: "assignments",
                schema: "delivery");
        }
    }
}
