using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Traccar.Protocols.Forward;
using Traccar.Server.Hubs;

namespace Traccar.Server.Forward;

/// <summary>
/// Broadcasts every decoded position to <see cref="TelemetryHub"/>'s connected, authenticated
/// clients — the group scoped to the position's device's ClientId, plus the administrators group
/// (which sees everything). Devices with no ClientId assigned are only visible to administrators.
/// Registered as (part of) the single <see cref="IPositionForwarder"/> alongside whatever external
/// broker Forward:Type selects — see Program.cs.
///
/// Method name and payload shape ("BroadcastMessage", a JSON-serialized string of
/// <see cref="Model.LivePositionUpdate"/>) match the Blazor fleet-management frontend's
/// TelemetryHubService/Map/Index.razor exactly — it subscribes with
/// <c>Hub.On&lt;string&gt;("BroadcastMessage", ...)</c> and manually deserializes the string, so
/// this must NOT go through ASP.NET's camelCase-configured JSON options (hence the explicit
/// null options below, which preserves the PascalCase property names the frontend expects).
/// </summary>
public sealed class TelemetryHubPositionForwarder(IHubContext<TelemetryHub> hubContext) : IPositionForwarder
{
    public const string Method = "BroadcastMessage";

    public void Forward(PositionForwardData data, Action<bool, Exception?> resultHandler)
    {
        SendAsync(data).ContinueWith(task =>
            resultHandler(!task.IsFaulted, task.Exception?.GetBaseException()));
    }

    private Task SendAsync(PositionForwardData data)
    {
        var device = data.Device;
        if (device == null)
        {
            return Task.CompletedTask;
        }

        var update = LiveTelemetryMapper.ToLivePositionUpdate(data.Position, device);
        var json = JsonSerializer.Serialize(update, (JsonSerializerOptions?)null);

        var tasks = new List<Task> { hubContext.Clients.Group(TelemetryHub.AdministratorsGroup).SendAsync(Method, json) };
        if (device.ClientId > 0)
        {
            tasks.Add(hubContext.Clients.Group(TelemetryHub.ClientGroup(device.ClientId)).SendAsync(Method, json));
        }
        return Task.WhenAll(tasks);
    }
}
