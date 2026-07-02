namespace Traccar.Model;

/// <summary>Join entity for the tc_group_device permission table.</summary>
public sealed class GroupDevice
{
    public long GroupId { get; set; }
    public long DeviceId { get; set; }
}
