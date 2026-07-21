namespace Traccar.Model;

/// <summary>Persisted OAuth2-style refresh token — survives restarts (unlike an in-memory store)
/// and lets a token be revoked by deleting the row. Single-use: redeemed and rotated on refresh.</summary>
public class RefreshToken
{
    public long Id { get; set; }

    public string Token { get; set; } = string.Empty;

    public long UserId { get; set; }

    public DateTime ExpiresAt { get; set; }
}
