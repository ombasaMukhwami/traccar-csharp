using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Traccar.Storage.Migrations.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class MultiClientUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A plain type-cast ALTER COLUMN would turn e.g. 11 into the bare text "11", which
            // isn't valid JSON for the new List<int> converter (it needs "[11]") — wrap each
            // existing value in a single-element JSON array instead, preserving the data.
            migrationBuilder.Sql("""
                ALTER TABLE users ALTER COLUMN client_id TYPE text
                USING (CASE WHEN client_id IS NULL THEN NULL ELSE '[' || client_id::text || ']' END);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Inverse: pull the first element back out of the JSON array into a bare integer.
            migrationBuilder.Sql("""
                ALTER TABLE users ALTER COLUMN client_id TYPE integer
                USING (CASE WHEN client_id IS NULL THEN NULL ELSE (client_id::jsonb->>0)::integer END);
                """);
        }
    }
}
