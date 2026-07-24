using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Traccar.Storage.Migrations.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceAssetFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "agent",
                table: "devices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "asset_certificate_no",
                table: "devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "chasis_no",
                table: "devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "color",
                table: "devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_dis_associated",
                table: "devices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "make",
                table: "devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "owner_contact",
                table: "devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "owner_id",
                table: "devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "owner_name",
                table: "devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tag",
                table: "devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tracking_object",
                table: "devices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "vehicle_model",
                table: "devices",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "agent",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "asset_certificate_no",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "chasis_no",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "color",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "is_dis_associated",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "make",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "owner_contact",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "owner_name",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "tag",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "tracking_object",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "vehicle_model",
                table: "devices");
        }
    }
}
