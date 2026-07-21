namespace Traccar.Model;

public class AgentDetails
{
    public int Id { get; set; }

    public int ResellerId { get; set; }

    public string AgentName { get; set; } = string.Empty;

    public string? AgentId { get; set; }

    public string? AgentPhoneNumber { get; set; }

    public string? Location { get; set; }
}
