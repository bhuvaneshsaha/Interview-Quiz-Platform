using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewQuiz.Openings.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MapOpeningPeopleToUserIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Map stored emails to identity user ids. Values that do not match a user stay as-is
            // so a later edit can replace them. Safe when the access schema is not present yet.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                  IF to_regclass('access.identity_users') IS NULL THEN
                    RETURN;
                  END IF;

                  UPDATE openings.openings AS opening
                  SET "Owner" = users."Id"
                  FROM access.identity_users AS users
                  WHERE lower(btrim(opening."Owner")) = lower(users."Email")
                    AND opening."Owner" IS DISTINCT FROM users."Id";

                  UPDATE openings.openings AS opening
                  SET "Handlers" = COALESCE((
                    SELECT jsonb_agg(to_jsonb(COALESCE(matched."Id", elem.value)) ORDER BY elem.ord)
                    FROM jsonb_array_elements_text(opening."Handlers") WITH ORDINALITY AS elem(value, ord)
                    LEFT JOIN access.identity_users AS matched
                      ON matched."Id" = btrim(elem.value)
                      OR lower(matched."Email") = lower(btrim(elem.value))
                  ), '[]'::jsonb)
                  WHERE jsonb_typeof(opening."Handlers") = 'array';
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // User ids cannot be turned back into the original free-text values.
        }
    }
}
