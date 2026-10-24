using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Core.Security;

public record AdmissionProof(string Id);

public sealed class AdmissionValidator(IConfiguration config, IWebHostEnvironment environment)
{
    public string? Validate(string token,Guid studentId,int examId) => ValidateProof(token,studentId,examId)?.Id;
    public AdmissionProof? ValidateProof(string token, Guid studentId, int examId)
    {
        var normal=ValidateToken(token,studentId,examId,false);
        if(normal is not null) return new(normal);
#if EXAMGUARDIAN_DEVELOPMENT
        if (environment.IsDevelopment() && config.GetValue<bool>("Development:AllowIdentityBypass")) {
            var development = ValidateToken(token, studentId, examId, true);
            if (development is not null) return new(development);
        }
#endif
        return null;
    }
    private string? ValidateToken(string token, Guid studentId, int examId, bool development)
    {
        try
        {
            var principal = new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(token, new TokenValidationParameters {
                ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)),
                ValidateIssuer = true, ValidIssuer = config["Jwt:Issuer"], ValidateAudience = true, ValidAudience = development ? "ExamGuardianDevelopmentAdmission" : "ExamGuardianAdmission",
                ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(5), ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                NameClaimType = "sub"
            }, out _);
            if (principal.FindFirst("sub")?.Value != studentId.ToString() || principal.FindFirst("exam_id")?.Value != examId.ToString() || principal.FindFirst("purpose")?.Value != (development ? "development-admission" : "admission")) return null;
            if (development && principal.FindFirst("override")?.Value != "biometricAdmission") return null;
            var id=principal.FindFirst("jti")?.Value;
            return id is not null && (development ? id.StartsWith("dev_") : !id.StartsWith("dev_")) && System.Text.RegularExpressions.Regex.IsMatch(id,@"^[a-zA-Z0-9_-]{16,128}$") ? id : null;
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException) { return null; }
    }
}
