namespace Traccar.Model;

public class SimCard
{
    public int Id { get; set; }

    public int ResellerId { get; set; }

    public long SerialNumber { get; set; }

    public long Imsi { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;
}
