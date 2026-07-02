namespace Traccar.Model;

/// <summary>Join entity for the tc_user_group permission table.</summary>
public sealed class UserGroup
{
    public long UserId { get; set; }
    public long GroupId { get; set; }
}
