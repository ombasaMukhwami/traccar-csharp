using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Traccar.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tc_group_device",
                columns: table => new
                {
                    GroupId = table.Column<long>(type: "INTEGER", nullable: false),
                    DeviceId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tc_group_device", x => new { x.GroupId, x.DeviceId });
                });

            migrationBuilder.CreateTable(
                name: "tc_user_device",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    DeviceId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tc_user_device", x => new { x.UserId, x.DeviceId });
                });

            migrationBuilder.CreateTable(
                name: "tc_user_group",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    GroupId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tc_user_group", x => new { x.UserId, x.GroupId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tc_group_device");

            migrationBuilder.DropTable(
                name: "tc_user_device");

            migrationBuilder.DropTable(
                name: "tc_user_group");
        }
    }
}
