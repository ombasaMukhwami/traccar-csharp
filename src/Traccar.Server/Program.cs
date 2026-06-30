using Microsoft.EntityFrameworkCore;
using Traccar.Protocols;
using Traccar.Storage;

var builder = WebApplication.CreateBuilder(args);

// Default Timeout sets SQLite's busy-wait (in seconds) so concurrent writers from the protocol
// pipeline (device status updates, position saves) retry instead of failing immediately on lock contention.
var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=traccar.db;Default Timeout=5";
builder.Services.AddDbContextFactory<TraccarDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<TraccarDbContext>(sp => sp.GetRequiredService<IDbContextFactory<TraccarDbContext>>().CreateDbContext());

builder.Services.AddTraccarProtocols(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<TraccarDbContext>().Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();
app.MapControllers();

var serverManager = app.Services.GetRequiredService<ServerManager>();

app.Lifetime.ApplicationStarted.Register(() => serverManager.StartAsync().GetAwaiter().GetResult());
app.Lifetime.ApplicationStopping.Register(() => serverManager.StopAsync().GetAwaiter().GetResult());

app.Run();
