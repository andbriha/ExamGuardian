using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using DataAccessLayer.Entities;

namespace Core.Security;

public sealed class AttemptTokens(IConfiguration config)
{
    private SymmetricSecurityKey Key => new(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
    public string Issue(Guid student, int session, string clientId, bool evidence)
    {
        var purpose = evidence ? "evidence" : "attempt";
        var token = new JwtSecurityToken(config["Jwt:Issuer"], "ExamGuardian:" + purpose,
            [new Claim("sub",student.ToString()),new Claim("session_id",session.ToString()),new Claim("jti",clientId)],
            expires: DateTime.UtcNow.AddDays(evidence ? 7 : 1), signingCredentials: new SigningCredentials(Key,SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    public bool Validate(string? token, ExamSession session, bool evidence)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        try
        {
            var principal = new JwtSecurityTokenHandler {MapInboundClaims=false}.ValidateToken(token,new TokenValidationParameters {
                ValidateIssuerSigningKey=true, IssuerSigningKey=Key, ValidateIssuer=true,ValidIssuer=config["Jwt:Issuer"],
                ValidateAudience=true,ValidAudience="ExamGuardian:"+(evidence ? "evidence" : "attempt"), ValidateLifetime=true,
                ClockSkew=TimeSpan.FromSeconds(5), ValidAlgorithms=[SecurityAlgorithms.HmacSha256]
            },out _);
            return principal.FindFirst("sub")?.Value == session.StudentId.ToString() && principal.FindFirst("session_id")?.Value == session.Id.ToString()
                && principal.FindFirst("jti")?.Value == (evidence ? session.EvidenceClientId : session.ActiveClientId);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException) { return false; }
    }
}
