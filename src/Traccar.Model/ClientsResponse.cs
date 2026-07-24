namespace Traccar.Model;

/// <summary>Response envelope for administrative/v1/api/Clients/client's list route — port of
/// the Blazor frontend's ClientsResponse DTO, which expects the list wrapped rather than bare.</summary>
public class ClientsResponse
{
    public List<Client> ClientViewModel { get; set; } = [];
}
