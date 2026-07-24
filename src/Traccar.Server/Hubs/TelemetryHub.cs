using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Traccar.Storage;

namespace Traccar.Server.Hubs;

/// <summary>
/// Live position feed for authenticated web clients (e.g. the Blazor fleet-management frontend).
/// [Authorize] rejects the connection handshake itself for anyone without a valid cookie or JWT,
/// so only logged-in users ever hold a connection. On connect, each client joins one group per
/// <see cref="Model.User.ClientId"/> entry — administrators join <see cref="AdministratorsGroup"/>
/// instead and see every device, mirroring how ReportUtils already bypasses ClientId filtering for
/// them. See <see cref="Forward.TelemetryHubPositionForwarder"/> for what gets broadcast to these
/// groups.
/// </summary>
[Authorize]
public class TelemetryHub(TraccarDbContext db) : Hub
{
    public const string AdministratorsGroup = "administrators";

    public static string ClientGroup(int clientId) => $"client-{clientId}";

    public override async Task OnConnectedAsync()
    {
        var userIdClaim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (long.TryParse(userIdClaim, out var userId))
        {
            var user = await db.Users.FindAsync(userId);
            if (user is { Administrator: true })
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, AdministratorsGroup);
            }
            else
            {
                // One group per assigned client — a user working across several clients (see
                // User.ClientId) sees live updates for all of them. No ClientId and not an
                // administrator: connection stays in no group, matching ReportUtils'
                // "unassigned users see no devices" convention.
                foreach (var clientId in user?.ClientId ?? [])
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, ClientGroup(clientId));
                }
            }
        }

        await base.OnConnectedAsync();
    }
}
