namespace Traccar.Model;

public class User : ExtendedModel
{
    public string? Name { get; set; }

    private string email = string.Empty;

    public string Email
    {
        get => email;
        set => email = value.Trim();
    }

    public string? Phone { get; set; }

    public bool Readonly { get; set; }

    public bool Administrator { get; set; }

    public bool Disabled { get; set; }

    public DateTime? ExpirationTime { get; set; }

    public void SetPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return;
        }
        var result = Hashing.CreateHash(password);
        HashedPassword = result.Hash;
        Salt = result.Salt;
    }

    public string? HashedPassword { get; set; }

    public string? Salt { get; set; }

    public bool IsPasswordValid(string password) => Hashing.ValidatePassword(password, HashedPassword, Salt);
}
