using System.Security.Cryptography;
using DataAccessLayer.Entities;
using Microsoft.AspNetCore.DataProtection;

namespace Core.Security;

// Only institution-issued passwords can be recovered. Never recover a user's own password.
public sealed class IssuedCredentials(IDataProtectionProvider protection)
{
    private IDataProtector For(User student) => protection.CreateProtector("issued-student-password-v1", student.Id.ToString());
    public string Issue(User student)
    {
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        student.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        student.IssuedPasswordProtected = For(student).Protect(password);
        student.RefreshToken = null;
        student.RefreshTokenExpiryTime = null;
        return password;
    }
    public string? Read(User student)
    {
        if (student.IssuedPasswordProtected is null) return null;
        try {
            var password = For(student).Unprotect(student.IssuedPasswordProtected);
            return BCrypt.Net.BCrypt.Verify(password, student.PasswordHash) ? password : null;
        } catch (CryptographicException) { return null; }
    }
}
