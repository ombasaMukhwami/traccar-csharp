using Microsoft.EntityFrameworkCore;
using Traccar.Model;
using Traccar.Protocols;
using Traccar.Storage;

namespace Traccar.Server;

/// <summary>Applies pending migrations and seeds first-run data (example reseller/client tenants,
/// the default admin user) — runs once at startup, from Program.cs.</summary>
public static class DatabaseSeeder
{
    public static void Seed(WebApplication app, bool jwtSecretIsEphemeral)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TraccarDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        db.Database.Migrate();

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        if (jwtSecretIsEphemeral)
        {
            logger.LogWarning("Jwt:Secret is not configured — using a random key generated for this process. " +
                "Access tokens and refresh tokens issued now will stop validating after a restart. Set Jwt:Secret in production.");
        }

        if (!db.Clients.Any())
        {
            // Example reseller/client tenants — port of MockApi's DatabaseSeeder.SeedResellersAndClients.
            // Roots (ParentId null) are resellers; rows with ParentId are their clients.
            db.Clients.AddRange(
            [
                new Client { Id = 1, Name = "Kabrasoft Kenya", Url = "kabrasoft.co.ke", Email = "ops@kabrasoft.co.ke", Address = "Nairobi, Kenya", PhoneNumber = "+254700000001", PrimaryColor = "#30b14b", SecondaryColor = "#243b30", MapProvider = "GOOGLE" },
                new Client { Id = 2, Name = "Kabrasoft Tanzania", Url = "kabrasoft.co.tz", Email = "ops@kabrasoft.co.tz", Address = "Dar es Salaam, Tanzania", PhoneNumber = "+255700000002", PrimaryColor = "#1976d2", SecondaryColor = "#455a64", MapProvider = "OPEN_STREET" },
                new Client { Id = 11, Name = "Acme Logistics", Url = "acme.example.com", Email = "fleet@acme.example.com", Address = "Industrial Area, Nairobi", PhoneNumber = "+254700100001", ParentId = 1, IsDefault = true },
                new Client { Id = 12, Name = "Metro Cabs", Url = "metrocabs.example.com", Email = "ops@metrocabs.example.com", Address = "Westlands, Nairobi", PhoneNumber = "+254700100002", ParentId = 1 },
                new Client { Id = 13, Name = "Savannah Farms", Url = "savannahfarms.example.com", Email = "logistics@savannahfarms.example.com", Address = "Naivasha, Kenya", PhoneNumber = "+254700100003", ParentId = 1 },
                new Client { Id = 14, Name = "Coastal Freight", Url = "coastalfreight.example.com", Email = "dispatch@coastalfreight.example.com", Address = "Mombasa, Kenya", PhoneNumber = "+254700100004", ParentId = 2 },
            ]);
            db.SaveChanges();

            // The rows above set explicit ids, bypassing the identity sequence — bump it past the
            // highest seeded id so the next INSERT without an explicit Id doesn't collide with one
            // of these.
            var clientEntity = db.Model.FindEntityType(typeof(Client))!;
            var clientsTable = clientEntity.GetTableName();
            var idColumn = clientEntity.FindProperty(nameof(Client.Id))!.GetColumnName();
#pragma warning disable EF1002 // table/column names come from EF metadata, not user input
            db.Database.ExecuteSqlRaw(
                $"""SELECT setval(pg_get_serial_sequence('"{clientsTable}"', '{idColumn}'), (SELECT COALESCE(MAX("{idColumn}"), 1) FROM "{clientsTable}"))""");
#pragma warning restore EF1002
        }

        if (!db.Users.Any())
        {
            var email = configuration[ConfigKeys.Admin.Email] ?? "admin";
            var password = configuration[ConfigKeys.Admin.Password] ?? "admin";
            var admin = new User
            {
                Name = ConfigKeys.Auth.RoleAdministrator,
                Email = email,
                Administrator = true,
                // Scopes the seeded admin to the "Kabrasoft Kenya" reseller/"Acme Logistics" client
                // seeded above, so /auth/login (which requires both) works out of the box — the
                // Administrator flag already bypasses ClientId-based filtering everywhere else.
                ResellerId = 1,
                ClientId = [11],
                UserType = UserType.Administrator,
                // Full access on every route in the catalog — without this, JwtIssuer emits no
                // "route_access" claims at all, and the frontend's RouteAccessClaims (which grants
                // nothing by default, Administrator or not — see its own doc comment) hides every
                // page and disables every button even for this seeded admin.
                RouteAccess = User.DefaultRouteAccess(UserType.Administrator),
            };
            admin.SetPassword(password);
            db.Users.Add(admin);
            db.SaveChanges();

            logger.LogWarning("Seeded default admin user — email: {Email}, password: {Password}. Change this immediately.",
                email, password);
        }
        else
        {
            // Backfills RouteAccess (and UserType) for administrators seeded before either was
            // added above (or any other admin account that ended up with none) — otherwise
            // they'd get zero "route_access" claims and see a blank UI despite Administrator
            // being set.
            var unseededAdmins = db.Users
                .Where(u => u.Administrator)
                .AsEnumerable()
                .Where(u => u.RouteAccess is null or { Count: 0 } || u.UserType != UserType.Administrator)
                .ToList();

            if (unseededAdmins.Count > 0)
            {
                foreach (var user in unseededAdmins)
                {
                    user.UserType = UserType.Administrator;
                    if (user.RouteAccess is null or { Count: 0 })
                    {
                        user.RouteAccess = User.DefaultRouteAccess(UserType.Administrator);
                    }
                }
                db.SaveChanges();
            }
        }
    }
}
