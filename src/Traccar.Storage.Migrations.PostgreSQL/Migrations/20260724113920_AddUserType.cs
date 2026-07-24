using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Traccar.Storage.Migrations.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddUserType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "User" (not "") so existing rows get a value that actually round-trips through the
            // UserType enum's string conversion — an empty default would throw the moment EF
            // tried to read one of those rows back.
            migrationBuilder.AddColumn<string>(
                name: "user_type",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "User");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "user_type",
                table: "users");
        }
    }
}
