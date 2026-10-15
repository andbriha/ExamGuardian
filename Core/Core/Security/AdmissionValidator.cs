using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Core.Security;

public record AdmissionProof(string Id);

public sealed class AdmissionValidator(IConfiguration config)
{
    public string? Validate(string token,Guid studentId,int examId) => ValidateProof(token,studentId,examId)?.Id;
    public AdmissionProof? ValidateProof(string token, Guid studentId, int examId)
    {
        var normal=ValidateNormal(token,studentId,examId);
        if(normal is not null) return new(normal);
return null;
    }
    private string? ValidateNormal(string token, Guid studentId, int examId)
    {
        try
        {
            var principal = new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(token, new TokenValidationParameters {
                ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)),
                ValidateIssuer = true, ValidIssuer = config["Jwt:Issuer"], ValidateAudience = true, ValidAudience = "ExamGuardianAdmission",
                ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(5), ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                NameClaimType = "sub"
            }, out _);
            if (principal.FindFirst("sub")?.Value != studentId.ToString() || principal.FindFirst("exam_id")?.Value != examId.ToString() || principal.FindFirst("purpose")?.Value != "admission") return null;
            var id=principal.FindFirst("jti")?.Value;
            return id is not null && System.Text.RegularExpressions.Regex.IsMatch(id,@"^[a-zA-Z0-9_-]{16,128}$") ? id : null;
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException) { return null; }
    }
}
