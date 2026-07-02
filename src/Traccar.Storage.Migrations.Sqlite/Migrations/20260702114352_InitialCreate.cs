using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Traccar.Storage.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
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

            migrationBuilder.CreateTable(
                name: "tc_devices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CalendarId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    UniqueId = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpdate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PositionId = table.Column<long>(type: "INTEGER", nullable: false),
                    Phone = table.Column<string>(type: "TEXT", nullable: true),
                    Model = table.Column<string>(type: "TEXT", nullable: true),
                    Contact = table.Column<string>(type: "TEXT", nullable: true),
                    Category = table.Column<string>(type: "TEXT", nullable: true),
                    Disabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExpirationTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Attributes = table.Column<string>(type: "TEXT", nullable: false),
                    GroupId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tc_devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tc_events",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PositionId = table.Column<long>(type: "INTEGER", nullable: false),
                    GeofenceId = table.Column<long>(type: "INTEGER", nullable: false),
                    MaintenanceId = table.Column<long>(type: "INTEGER", nullable: false),
                    Attributes = table.Column<string>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<long>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tc_events", x => x.Id);
                });

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
                name: "tc_groups",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Attributes = table.Column<string>(type: "TEXT", nullable: false),
                    GroupId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tc_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tc_positions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Protocol = table.Column<string>(type: "TEXT", nullable: true),
                    ServerTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeviceTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FixTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Outdated = table.Column<bool>(type: "INTEGER", nullable: false),
                    Valid = table.Column<bool>(type: "INTEGER", nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: false),
                    Longitude = table.Column<double>(type: "REAL", nullable: false),
                    Altitude = table.Column<double>(type: "REAL", nullable: false),
                    Speed = table.Column<double>(type: "REAL", nullable: false),
                    Course = table.Column<double>(type: "REAL", nullable: false),
                    Address = table.Column<string>(type: "TEXT", nullable: true),
                    Accuracy = table.Column<double>(type: "REAL", nullable: false),
                    Network = table.Column<string>(type: "TEXT", nullable: true),
                    GeofenceIds = table.Column<string>(type: "TEXT", nullable: true),
                    Attributes = table.Column<string>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<long>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tc_positions", x => x.Id);
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

            migrationBuilder.CreateTable(
                name: "tc_users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    Phone = table.Column<string>(type: "TEXT", nullable: true),
                    Readonly = table.Column<bool>(type: "INTEGER", nullable: false),
                    Administrator = table.Column<bool>(type: "INTEGER", nullable: false),
                    Disabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExpirationTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HashedPassword = table.Column<string>(type: "TEXT", nullable: true),
                    Salt = table.Column<string>(type: "TEXT", nullable: true),
                    Attributes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tc_users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tc_commands_DeviceId",
                table: "tc_commands",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_tc_devices_UniqueId",
                table: "tc_devices",
                column: "UniqueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tc_events_DeviceId_EventTime",
                table: "tc_events",
                columns: new[] { "DeviceId", "EventTime" });

            migrationBuilder.CreateIndex(
                name: "IX_tc_positions_DeviceId_FixTime",
                table: "tc_positions",
                columns: new[] { "DeviceId", "FixTime" });

            migrationBuilder.CreateIndex(
                name: "IX_tc_users_Email",
                table: "tc_users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tc_commands");

            migrationBuilder.DropTable(
                name: "tc_devices");

            migrationBuilder.DropTable(
                name: "tc_events");

            migrationBuilder.DropTable(
                name: "tc_group_device");

            migrationBuilder.DropTable(
                name: "tc_groups");

            migrationBuilder.DropTable(
                name: "tc_positions");

            migrationBuilder.DropTable(
                name: "tc_user_device");

            migrationBuilder.DropTable(
                name: "tc_user_group");

            migrationBuilder.DropTable(
                name: "tc_users");
        }
    }
}
