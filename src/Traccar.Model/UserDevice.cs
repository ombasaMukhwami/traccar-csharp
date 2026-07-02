namespace Traccar.Model;

/// <summary>Join entity for the tc_user_device permission table.</summary>
public sealed class UserDevice
{
    public long UserId { get; set; }
    public long DeviceId { get; set; }
}
