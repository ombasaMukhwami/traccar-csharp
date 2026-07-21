using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using Traccar.Server;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using Traccar.Protocols;
using Traccar.Protocols.Forward;
using Traccar.Server.Auth;
using Traccar.Server.Forward;
using Traccar.Server.Hubs;
using Traccar.Server.Reports;
using Traccar.Storage;

const string TelemetryHubPath = "/hubs/telemetry";

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddDbContextFactory<TraccarDbContext>(options =>
    DbProviderExtensions.UseProvider(options, builder.Configuration));

builder.Services.AddScoped<TraccarDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<TraccarDbContext>>().CreateDbContext());

builder.Services.AddTraccarProtocols(builder.Configuration);

builder.Services.AddSignalR();

// TelemetryHub broadcasts every decoded position to authenticated, client-scoped web clients (see
// TelemetryHubPositionForwarder). It runs alongside whatever external broker Forward:Type selects
// (Kafka/RabbitMQ/a remote SignalR hub) rather than replacing it, via CompositePositionForwarder —
// this registration deliberately comes after AddTraccarProtocols so it wins the single
// IPositionForwarder slot that BaseProtocol resolves.
builder.Services.AddSingleton<IPositionForwarder>(sp =>
{
    var externalForwarder = DependencyInjection.CreateConfiguredForwarder(builder.Configuration);
    var hubForwarder = new TelemetryHubPositionForwarder(sp.GetRequiredService<IHubContext<TelemetryHub>>());
    return externalForwarder == null
        ? hubForwarder
        : new CompositePositionForwarder([externalForwarder, hubForwarder]);
});

builder.Services.AddScoped<ReportUtils>();
builder.Services.AddScoped<TripsReportProvider>();
builder.Services.AddScoped<StopsReportProvider>();
builder.Services.AddScoped<SummaryReportProvider>();
builder.Services.AddScoped<DevicesReportProvider>();

// JWT bearer auth serves API clients (e.g. the Blazor fleet-management frontend) that carry their
// own access token instead of a cookie. Both schemes are registered; a policy scheme picks
// whichever applies per-request based on the presence of an Authorization: Bearer header, so
// [Authorize] works unmodified against either kind of caller.
var jwtOptions = JwtOptions.Bind(builder.Configuration);
var jwtSecretIsEphemeral = string.IsNullOrEmpty(jwtOptions.Secret);
var jwtSigningKey = new SymmetricSecurityKey(
    jwtSecretIsEphemeral ? RandomNumberGenerator.GetBytes(64) : Encoding.UTF8.GetBytes(jwtOptions.Secret!));

builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton(jwtSigningKey);
builder.Services.AddScoped<JwtIssuer>();

const string AuthSelectorScheme = "TraccarAuthSelector";

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = AuthSelectorScheme;
    options.DefaultChallengeScheme = AuthSelectorScheme;
})
    .AddPolicyScheme(AuthSelectorScheme, "Cookie or Bearer", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var header = context.Request.Headers.Authorization.FirstOrDefault();
            var hasBearerHeader = header?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true;

            // Browsers can't set an Authorization header on a WebSocket upgrade, so SignalR's
            // JS client falls back to putting the token in the query string for the hub
            // connection itself (the preceding negotiate POST still uses the header above).
            var hasQueryToken = context.Request.Path.StartsWithSegments(TelemetryHubPath) &&
                !string.IsNullOrEmpty(context.Request.Query["access_token"]);

            return hasBearerHeader || hasQueryToken
                ? JwtBearerDefaults.AuthenticationScheme
                : CookieAuthenticationDefaults.AuthenticationScheme;
        };
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
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
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwtSigningKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
        // JwtBearer only reads the Authorization header by default — teach it to also accept
        // the token from the query string for the hub connection itself (see the policy-scheme
        // selector above for why that's necessary).
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments(TelemetryHubPath))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
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

DatabaseSeeder.Seed(app, jwtSecretIsEphemeral);

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
app.MapHub<TelemetryHub>(TelemetryHubPath);

var serverManager = app.Services.GetRequiredService<ServerManager>();

app.Lifetime.ApplicationStarted.Register(() => serverManager.StartAsync().GetAwaiter().GetResult());
app.Lifetime.ApplicationStopping.Register(() => serverManager.StopAsync().GetAwaiter().GetResult());

app.Run();
