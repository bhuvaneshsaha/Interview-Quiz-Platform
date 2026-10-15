using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewQuiz.Delivery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StampQuizRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "QuizRowVersion",
                schema: "delivery",
                table: "assignment_snapshots",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql(
                """
                UPDATE delivery.assignment_snapshots
                SET "QuizRowVersion" = CASE
                  WHEN "Payload" ? 'rowVersion'
                    AND ("Payload"->>'rowVersion') ~ '^[0-9]+$'
                  THEN ("Payload"->>'rowVersion')::bigint
                  ELSE 0
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuizRowVersion",
                schema: "delivery",
                table: "assignment_snapshots");
        }
    }
}
