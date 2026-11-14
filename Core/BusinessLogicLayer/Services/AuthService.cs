using BusinessLogicLayer.Common;
using BusinessLogicLayer.Dtos.Auth;
using BusinessLogicLayer.Interfaces;
using BusinessLogicLayer.Security;
using DataAccessLayer.Data;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace BusinessLogicLayer.Services;
public class AuthService(AppDbContext db, IConfiguration config, AccessTokenKeys keys) : IAuthService {
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime Start, int Count)> attempts = new();
    private static readonly object attemptLock = new();
    private static bool AdmitLogin(string login) {
        var now = DateTime.UtcNow;
        lock (attemptLock) {
            foreach (var item in attempts.Where(x => now - x.Value.Start > TimeSpan.FromMinutes(5)).ToArray()) attempts.TryRemove(item.Key, out _);
            var key = Hash(login.Trim().ToUpperInvariant());
            if (!attempts.TryGetValue(key, out var value)) {
                if (attempts.Count >= 10000) return false;
                value = (now, 0);
            }
            if (value.Count >= 10) return false;
            attempts[key] = (value.Start, value.Count + 1); return true;
        }
    }
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public async Task<Result<AuthResponseDto>> LoginAsync(LoginRequestDto request) {
        if (string.IsNullOrWhiteSpace(request.Login) || request.Login.Length > 100 || string.IsNullOrEmpty(request.Password) || request.Password.Length > 256)
            return Result.Failure<AuthResponseDto>("Invalid credentials");
        if (!AdmitLogin(request.Login)) return Result.Failure<AuthResponseDto>("Too many attempts. Retry later.");
        var user = await db.Users.SingleOrDefaultAsync(u => u.Login == request.Login);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)) return Result.Failure<AuthResponseDto>("Invalid credentials");
        attempts.TryRemove(Hash(request.Login.Trim().ToUpperInvariant()), out _);
        var session = new AuthSession { Id = Guid.NewGuid(), UserId = user.Id, User = user, CredentialStamp = Hash(user.PasswordHash), ExpiresAt = DateTime.UtcNow.AddDays(7) };
        db.AuthSessions.Add(session);
        var result = Issue(user, session);
        await db.SaveChangesAsync();
        return result;
    }
    public async Task<Result<AuthResponseDto>> RefreshTokenAsync(string currentRefreshToken) {
        if (currentRefreshToken.Length > 200) return Result.Failure<AuthResponseDto>("Invalid refresh token");
        var hash = Hash(currentRefreshToken);
        var credential = await db.RefreshCredentials.AsNoTracking().SingleOrDefaultAsync(t => t.Hash == hash);
        if (credential is null) return Result.Failure<AuthResponseDto>("Invalid or expired refresh token");
        await using var tx = await db.Database.BeginTransactionAsync();
        var session = await db.AuthSessions.FromSqlInterpolated($"SELECT * FROM \"AuthSessions\" WHERE \"Id\" = {credential.SessionId} FOR UPDATE").SingleAsync();
        var current = await db.RefreshCredentials.SingleAsync(t => t.Hash == hash);
        var user = await db.Users.SingleAsync(u => u.Id == session.UserId);
        if (current.ConsumedAt is not null || session.RevokedAt is not null || session.ExpiresAt <= DateTime.UtcNow || session.CredentialStamp != Hash(user.PasswordHash)) {
            session.RevokedAt ??= DateTime.UtcNow;
            await db.SaveChangesAsync(); await tx.CommitAsync();
            return Result.Failure<AuthResponseDto>("Invalid or reused refresh token");
        }
        current.ConsumedAt = DateTime.UtcNow;
        var result = Issue(user, session);
        await db.SaveChangesAsync(); await tx.CommitAsync();
        return result;
    }
    public async Task RevokeAsync(string? refreshToken, Guid? sessionId) {
        if (refreshToken is { Length: <= 200 }) {
            var hash = Hash(refreshToken);
            sessionId = await db.RefreshCredentials.Where(t => t.Hash == hash).Select(t => (Guid?)t.SessionId).SingleOrDefaultAsync() ?? sessionId;
        }
        if (sessionId is not null) await db.AuthSessions.Where(s => s.Id == sessionId).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTime.UtcNow));
    }
    private Result<AuthResponseDto> Issue(User user, AuthSession session) {
        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        db.RefreshCredentials.Add(new RefreshCredential { Hash = Hash(refresh), SessionId = session.Id });
        var lifetime = int.TryParse(config["Jwt:LifetimeMinutes"], out var minutes) ? Math.Clamp(minutes, 1, 30) : 15;
        var token = new JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"], [
            new Claim("sub", user.Id.ToString()), new Claim("id", user.Id.ToString()), new Claim("sid", session.Id.ToString()),
            new Claim("jti", Guid.NewGuid().ToString("N")), new Claim(ClaimTypes.NameIdentifier, user.Login),
            new Claim("login", user.Login), new Claim(ClaimTypes.Role, user.Role), new Claim("fullName", user.FullName)
        ], expires: DateTime.UtcNow.AddMinutes(lifetime), signingCredentials: new SigningCredentials(keys.Key, SecurityAlgorithms.RsaSha256));
        return Result.Success(new AuthResponseDto { AccessToken = new JwtSecurityTokenHandler().WriteToken(token), ExpiresAt = token.ValidTo,
            Login = user.Login, Role = user.Role, RefreshToken = refresh });
    }
}
