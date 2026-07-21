using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Traccar.Storage.Migrations.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agents",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reseller_id = table.Column<int>(type: "integer", nullable: false),
                    agent_name = table.Column<string>(type: "text", nullable: false),
                    agent_id = table.Column<string>(type: "text", nullable: true),
                    agent_phone_number = table.Column<string>(type: "text", nullable: true),
                    location = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    url = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    parent_id = table.Column<int>(type: "integer", nullable: true),
                    primary_color = table.Column<string>(type: "text", nullable: true),
                    secondary_color = table.Column<string>(type: "text", nullable: true),
                    map_provider = table.Column<string>(type: "text", nullable: false),
                    mapbox_access_token = table.Column<string>(type: "text", nullable: true),
                    google_maps_api_key = table.Column<string>(type: "text", nullable: true),
                    device_limit = table.Column<int>(type: "integer", nullable: false, defaultValue: 110),
                    time_zone = table.Column<string>(type: "text", nullable: false),
                    default_latitude = table.Column<double>(type: "double precision", nullable: true),
                    default_longitude = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clients", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "commands",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    text_channel = table.Column<bool>(type: "boolean", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    attributes = table.Column<string>(type: "text", nullable: false),
                    device_id = table.Column<long>(type: "bigint", nullable: false),
                    type = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_commands", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "device_attributes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    device_id = table.Column<long>(type: "bigint", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    attribute = table.Column<string>(type: "text", nullable: true),
                    expression = table.Column<string>(type: "text", nullable: true),
                    type = table.Column<string>(type: "text", nullable: true),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    attributes = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_attributes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "device_models",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    model = table.Column<string>(type: "text", nullable: false),
                    manufacturer = table.Column<string>(type: "text", nullable: true),
                    port = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_models", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "devices",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: true),
                    unique_id = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    last_update = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    position_id = table.Column<long>(type: "bigint", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    model = table.Column<string>(type: "text", nullable: true),
                    disabled = table.Column<bool>(type: "boolean", nullable: false),
                    expiration_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    client_id = table.Column<int>(type: "integer", nullable: false),
                    serial_no = table.Column<string>(type: "text", nullable: true),
                    attributes = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_devices", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "event_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    event_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    position_id = table.Column<long>(type: "bigint", nullable: false),
                    geofence_id = table.Column<long>(type: "bigint", nullable: false),
                    maintenance_id = table.Column<long>(type: "bigint", nullable: false),
                    attributes = table.Column<string>(type: "text", nullable: false),
                    device_id = table.Column<long>(type: "bigint", nullable: false),
                    type = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "positions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    protocol = table.Column<string>(type: "text", nullable: true),
                    server_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    device_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fix_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    outdated = table.Column<bool>(type: "boolean", nullable: false),
                    valid = table.Column<bool>(type: "boolean", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    altitude = table.Column<double>(type: "double precision", nullable: false),
                    speed = table.Column<double>(type: "double precision", nullable: false),
                    course = table.Column<double>(type: "double precision", nullable: false),
                    address = table.Column<string>(type: "text", nullable: true),
                    accuracy = table.Column<double>(type: "double precision", nullable: false),
                    network = table.Column<string>(type: "text", nullable: true),
                    geofence_ids = table.Column<string>(type: "text", nullable: true),
                    ignition = table.Column<bool>(type: "boolean", nullable: false),
                    event_id = table.Column<int>(type: "integer", nullable: false),
                    odometer = table.Column<double>(type: "double precision", nullable: false),
                    attributes = table.Column<string>(type: "text", nullable: false),
                    device_id = table.Column<long>(type: "bigint", nullable: false),
                    type = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_positions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "routes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    path = table.Column<string>(type: "text", nullable: false),
                    label = table.Column<string>(type: "text", nullable: false),
                    group_name = table.Column<string>(type: "text", nullable: false),
                    supports_add = table.Column<bool>(type: "boolean", nullable: false),
                    supports_edit = table.Column<bool>(type: "boolean", nullable: false),
                    supports_delete = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_routes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sim_cards",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reseller_id = table.Column<int>(type: "integer", nullable: false),
                    serial_number = table.Column<long>(type: "bigint", nullable: false),
                    imsi = table.Column<long>(type: "bigint", nullable: false),
                    phone_number = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sim_cards", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: false),
                    phone = table.Column<string>(type: "text", nullable: true),
                    @readonly = table.Column<bool>(name: "readonly", type: "boolean", nullable: false),
                    administrator = table.Column<bool>(type: "boolean", nullable: false),
                    disabled = table.Column<bool>(type: "boolean", nullable: false),
                    is_locked_out = table.Column<bool>(type: "boolean", nullable: false),
                    expiration_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    client_id = table.Column<int>(type: "integer", nullable: true),
                    reseller_id = table.Column<int>(type: "integer", nullable: true),
                    route_access = table.Column<string>(type: "text", nullable: true),
                    hashed_password = table.Column<string>(type: "text", nullable: true),
                    salt = table.Column<string>(type: "text", nullable: true),
                    attributes = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "device_models",
                columns: new[] { "id", "manufacturer", "model", "port" },
                values: new object[,]
                {
                    { 1, "Xexun", "TK-103", "COM1" },
                    { 2, "Concox", "GT-06", "COM2" },
                    { 3, "Concox", "Concox JM01", "COM3" },
                    { 4, "Teltonika", "Teltonika FMB920", "COM4" }
                });

            migrationBuilder.InsertData(
                table: "event_types",
                columns: new[] { "id", "description", "name" },
                values: new object[,]
                {
                    { 1, "General-purpose alarm with no specific category.", "general" },
                    { 2, "Emergency SOS alert triggered manually by the driver.", "sos" },
                    { 3, "Vibration or shock detected on the device or vehicle.", "vibration" },
                    { 4, "Unexpected vehicle movement detected while ignition is off.", "movement" },
                    { 5, "Vehicle speed dropped below the configured minimum threshold.", "lowspeed" },
                    { 6, "Vehicle speed exceeded the configured maximum threshold.", "overspeed" },
                    { 7, "Free-fall or sudden impact detected; used primarily on wearable and asset trackers.", "fallDown" },
                    { 8, "External power supply voltage has fallen below the safe operating level.", "lowPower" },
                    { 9, "Internal battery charge is critically low.", "lowBattery" },
                    { 10, "General device or vehicle fault condition reported.", "fault" },
                    { 11, "External power supply to the tracker was disconnected.", "powerOff" },
                    { 12, "External power supply to the tracker was reconnected.", "powerOn" },
                    { 13, "Door opened or closed unexpectedly.", "door" },
                    { 14, "Vehicle or asset lock was activated.", "lock" },
                    { 15, "Vehicle or asset lock was deactivated.", "unlock" },
                    { 16, "Geofence boundary was crossed (generic, direction unspecified).", "geofence" },
                    { 17, "Vehicle entered a defined geofence zone.", "geofenceEnter" },
                    { 18, "Vehicle exited a defined geofence zone.", "geofenceExit" },
                    { 19, "GPS antenna cable has been cut or disconnected — possible tampering.", "gpsAntennaCut" },
                    { 20, "Collision or high-G impact detected.", "accident" },
                    { 21, "Vehicle is being towed while the ignition is off.", "tow" },
                    { 22, "Engine has been running with no movement for longer than the configured idle limit.", "idle" },
                    { 23, "Engine RPM exceeded the configured threshold.", "highRpm" },
                    { 24, "Sudden hard-acceleration event detected.", "hardAcceleration" },
                    { 25, "Sudden hard-braking event detected.", "hardBraking" },
                    { 26, "Sharp cornering or hard-turning manoeuvre detected.", "hardCornering" },
                    { 27, "Abrupt lane-change manoeuvre detected.", "laneChange" },
                    { 28, "Driver has exceeded continuous driving time beyond the configured fatigue limit.", "fatigueDriving" },
                    { 29, "Main power supply to the tracker was severed — likely tampering.", "powerCut" },
                    { 30, "Main power supply to the tracker was restored after a cut.", "powerRestored" },
                    { 31, "GPS or GSM signal jamming detected in the vicinity.", "jamming" },
                    { 32, "Monitored temperature exceeded the configured high or low threshold.", "temperature" },
                    { 33, "Vehicle entered or exited a parking state.", "parking" },
                    { 34, "Vehicle bonnet (hood) was opened.", "bonnet" },
                    { 35, "Foot brake or parking brake was applied.", "footBrake" },
                    { 36, "Fuel level drop inconsistent with normal consumption — possible leak or theft.", "fuelLeak" },
                    { 37, "Physical tampering or device case opening detected.", "tampering" },
                    { 38, "Device is being removed from the vehicle.", "removing" }
                });

            migrationBuilder.InsertData(
                table: "routes",
                columns: new[] { "id", "group_name", "label", "path", "supports_add", "supports_delete", "supports_edit" },
                values: new object[,]
                {
                    { 1, "General", "Fleet", "map", false, false, false },
                    { 2, "General", "Dashboard", "dashboard", false, false, false },
                    { 3, "General", "Geozones", "geozones", true, true, true },
                    { 4, "General", "Alerts", "alerts", true, true, true },
                    { 5, "General", "History", "trips", false, false, false },
                    { 6, "Administrative", "Users", "admin/users", true, true, true },
                    { 7, "Administrative", "Devices", "admin/devices", true, true, true },
                    { 8, "Administrative", "Move Devices", "admin/move-devices", false, false, true },
                    { 9, "Administrative", "SIM Cards", "admin/sim-cards", true, true, true },
                    { 10, "Administrative", "Clients", "admin/clients", true, true, true },
                    { 11, "Administrative", "Agents", "admin/agents", true, true, true },
                    { 12, "Administrative", "Resellers", "admin/resellers", true, true, true },
                    { 13, "Administrative", "Database", "admin/database", false, false, false }
                });

            migrationBuilder.CreateIndex(
                name: "ix_commands_device_id",
                table: "commands",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_attributes_device_id",
                table: "device_attributes",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "ix_devices_unique_id",
                table: "devices",
                column: "unique_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_events_device_id_event_time",
                table: "events",
                columns: new[] { "device_id", "event_time" });

            migrationBuilder.CreateIndex(
                name: "ix_positions_device_id_fix_time",
                table: "positions",
                columns: new[] { "device_id", "fix_time" });

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agents");

            migrationBuilder.DropTable(
                name: "clients");

            migrationBuilder.DropTable(
                name: "commands");

            migrationBuilder.DropTable(
                name: "device_attributes");

            migrationBuilder.DropTable(
                name: "device_models");

            migrationBuilder.DropTable(
                name: "devices");

            migrationBuilder.DropTable(
                name: "event_types");

            migrationBuilder.DropTable(
                name: "events");

            migrationBuilder.DropTable(
                name: "positions");

            migrationBuilder.DropTable(
                name: "routes");

            migrationBuilder.DropTable(
                name: "sim_cards");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
