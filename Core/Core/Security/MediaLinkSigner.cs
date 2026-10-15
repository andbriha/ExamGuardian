using System.Security.Cryptography;
using System.Text;
namespace Core.Security;
public sealed class MediaLinkSigner(IConfiguration config)
{
    private byte[] Key => Encoding.UTF8.GetBytes(config["Jwt:Key"] ?? throw new InvalidOperationException("JWT key missing"));
    public string Sign(int sessionId, int chunkIndex, long expires, string stream = "camera") => Convert.ToHexString(HMACSHA256.HashData(Key, Encoding.UTF8.GetBytes($"video:{sessionId}:{chunkIndex}:{expires}:{stream}")));
    public bool Validate(int sessionId, int chunkIndex, long expires, string? signature, string stream = "camera")
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (expires < now || expires > now + 900 || signature?.Length != 64) return false;
        try { return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(Sign(sessionId, chunkIndex, expires, stream)), Convert.FromHexString(signature)); }
        catch (FormatException) { return false; }
    }
}
