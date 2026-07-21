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
/// </summary>
public sealed class TelemetryHubPositionForwarder(IHubContext<TelemetryHub> hubContext) : IPositionForwarder
{
    public const string Method = "PositionReceived";

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

        var tasks = new List<Task> { hubContext.Clients.Group(TelemetryHub.AdministratorsGroup).SendAsync(Method, data) };
        if (device.ClientId > 0)
        {
            tasks.Add(hubContext.Clients.Group(TelemetryHub.ClientGroup(device.ClientId)).SendAsync(Method, data));
        }
        return Task.WhenAll(tasks);
    }
}
