using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Controllers;
using Traccar.Server;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using Traccar.Model;
using Traccar.Protocols;
using Traccar.Server.Reports;
using Traccar.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=traccar.db;Default Timeout=5";

var provider = (builder.Configuration[ConfigKeys.Database.Provider] ?? "sqlite").ToLowerInvariant();

var retry = builder.Configuration.GetSection("Database:Retry").Get<DatabaseRetryOptions>() ?? new DatabaseRetryOptions();

builder.Services.AddDbContextFactory<TraccarDbContext>(options =>
    DbProviderExtensions.UseProvider(options, provider, connectionString, retry));

builder.Services.AddScoped<TraccarDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<TraccarDbContext>>().CreateDbContext());

builder.Services.AddTraccarProtocols(builder.Configuration);

builder.Services.AddScoped<ReportUtils>();
builder.Services.AddScoped<TripsReportProvider>();
builder.Services.AddScoped<StopsReportProvider>();
builder.Services.AddScoped<SummaryReportProvider>();
builder.Services.AddScoped<DevicesReportProvider>();

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
