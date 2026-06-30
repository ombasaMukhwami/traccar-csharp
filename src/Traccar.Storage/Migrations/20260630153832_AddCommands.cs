using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Traccar.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddCommands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tc_commands",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TextChannel = table.Column<bool>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Attributes = table.Column<string>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<long>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tc_commands", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tc_commands_DeviceId",
                table: "tc_commands",
                column: "DeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tc_commands");
        }
    }
}
