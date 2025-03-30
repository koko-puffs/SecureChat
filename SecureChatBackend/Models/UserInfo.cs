namespace SecureChatBackend.Models;

public class UserInfo
{
    public string ConnectionId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    // Store public key as JWK (JSON Web Key) string format for easy JS interop
    public string PublicKey { get; set; } = string.Empty;
}