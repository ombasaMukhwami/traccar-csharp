using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using Traccar.Model;
using Traccar.Protocols;
using Traccar.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

// Default Timeout sets SQLite's busy-wait (in seconds) so concurrent writers from the protocol
// pipeline (device status updates, position saves) retry instead of failing immediately on lock contention.
var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=traccar.db;Default Timeout=5";
builder.Services.AddDbContextFactory<TraccarDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<TraccarDbContext>(sp => sp.GetRequiredService<IDbContextFactory<TraccarDbContext>>().CreateDbContext());

builder.Services.AddTraccarProtocols(builder.Configuration);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = ConfigKeys.Auth.CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        // Return 401/403 instead of redirecting — this is a JSON API, not a browser app.
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddOpenApi(options =>
{
    // Scalar needs operationId on every operation to generate client code snippets.
    // ASP.NET Core's AddOpenApi() leaves it empty by default, so we derive it from
    // the controller and action names: e.g. "Devices_GetById", "Session_Login".
    options.AddOperationTransformer((operation, context, ct) =>
    {
        if (context.Description.ActionDescriptor is ControllerActionDescriptor cad)
        {
            operation.OperationId = $"{cad.ControllerName}_{cad.ActionName}";
        }
        return Task.CompletedTask;
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TraccarDbContext>();
    db.Database.Migrate();

    if (!db.Users.Any())
    {
        var email = builder.Configuration[ConfigKeys.Admin.Email] ?? "admin";
        var password = builder.Configuration[ConfigKeys.Admin.Password] ?? "admin";
        var admin = new User { Name = ConfigKeys.Auth.RoleAdministrator, Email = email, Administrator = true };
        admin.SetPassword(password);
        db.Users.Add(admin);
        db.SaveChanges();

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning("Seeded default admin user — email: {Email}, password: {Password}. Change this immediately.",
            email, password);
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "Traccar C# API";
        options.Theme = ScalarTheme.BluePlanet;
        options.DefaultHttpClient = new(ScalarTarget.JavaScript, ScalarClient.Fetch);
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

var serverManager = app.Services.GetRequiredService<ServerManager>();

app.Lifetime.ApplicationStarted.Register(() => serverManager.StartAsync().GetAwaiter().GetResult());
app.Lifetime.ApplicationStopping.Register(() => serverManager.StopAsync().GetAwaiter().GetResult());

app.Run();
