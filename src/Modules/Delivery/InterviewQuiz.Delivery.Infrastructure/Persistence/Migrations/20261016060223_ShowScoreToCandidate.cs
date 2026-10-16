using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewQuiz.Delivery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShowScoreToCandidate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowScoreToCandidate",
                schema: "delivery",
                table: "assignments",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShowScoreToCandidate",
                schema: "delivery",
                table: "assignments");
        }
    }
}
