using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
namespace BusinessLogicLayer.Security;

// Only Core owns the private key. All verifier services consume the public JWKS.
public sealed class AccessTokenKeys : IDisposable {
    private readonly RSA rsa = RSA.Create();
    public RsaSecurityKey Key { get; }
    public AccessTokenKeys(string directory) {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "access-signing.pem");
        if (!File.Exists(path)) {
            using var generated = RSA.Create(3072);
            var temporary = path + "." + Guid.NewGuid().ToString("N");
            File.WriteAllText(temporary, generated.ExportPkcs8PrivateKeyPem());
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            try { File.Move(temporary, path, false); }
            catch (IOException) when (File.Exists(path)) { File.Delete(temporary); }
        }
        rsa.ImportFromPem(File.ReadAllText(path));
        Key = new RsaSecurityKey(rsa) { KeyId = Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()))[..24] };
    }
    public object PublicJwks() {
        var p = rsa.ExportParameters(false);
        return new { keys = new[] { new { kty = "RSA", use = "sig", alg = "RS256", kid = Key.KeyId, n = Base64UrlEncoder.Encode(p.Modulus!), e = Base64UrlEncoder.Encode(p.Exponent!) } } };
    }
    public void Dispose() => rsa.Dispose();
}
