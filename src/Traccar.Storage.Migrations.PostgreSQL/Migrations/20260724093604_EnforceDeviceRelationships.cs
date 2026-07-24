using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Traccar.Storage.Migrations.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class EnforceDeviceRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tag",
                table: "devices");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_devices_client_id",
                table: "devices",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_devices_position_id",
                table: "devices",
                column: "position_id");

            migrationBuilder.AddForeignKey(
                name: "fk_commands_devices_device_id",
                table: "commands",
                column: "device_id",
                principalTable: "devices",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_device_attributes_devices_device_id",
                table: "device_attributes",
                column: "device_id",
                principalTable: "devices",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_devices_clients_client_id",
                table: "devices",
                column: "client_id",
                principalTable: "clients",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_devices_positions_position_id",
                table: "devices",
                column: "position_id",
                principalTable: "positions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_events_devices_device_id",
                table: "events",
                column: "device_id",
                principalTable: "devices",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_positions_devices_device_id",
                table: "positions",
                column: "device_id",
                principalTable: "devices",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_refresh_tokens_users_user_id",
                table: "refresh_tokens",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_commands_devices_device_id",
                table: "commands");

            migrationBuilder.DropForeignKey(
                name: "fk_device_attributes_devices_device_id",
                table: "device_attributes");

            migrationBuilder.DropForeignKey(
                name: "fk_devices_clients_client_id",
                table: "devices");

            migrationBuilder.DropForeignKey(
                name: "fk_devices_positions_position_id",
                table: "devices");

            migrationBuilder.DropForeignKey(
                name: "fk_events_devices_device_id",
                table: "events");

            migrationBuilder.DropForeignKey(
                name: "fk_positions_devices_device_id",
                table: "positions");

            migrationBuilder.DropForeignKey(
                name: "fk_refresh_tokens_users_user_id",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_user_id",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "ix_devices_client_id",
                table: "devices");

            migrationBuilder.DropIndex(
                name: "ix_devices_position_id",
                table: "devices");

            migrationBuilder.AddColumn<string>(
                name: "tag",
                table: "devices",
                type: "text",
                nullable: true);
        }
    }
}
