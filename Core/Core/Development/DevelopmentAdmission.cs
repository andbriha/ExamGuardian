#if EXAMGUARDIAN_DEVELOPMENT
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
namespace Core.Development;

// Server authorization is independent of the client's local DEV switches.
public sealed class DevelopmentAdmission(IConfiguration config, IWebHostEnvironment environment)
{
    public const string Control = "biometricAdmission";
    public bool Enabled => environment.IsDevelopment() && config.GetValue<bool>("Development:AllowIdentityBypass");
    public string Issue(Guid studentId, int examId)
    {
        if (!Enabled) throw new InvalidOperationException("Development identity bypass is disabled.");
        var token = new JwtSecurityToken(config["Jwt:Issuer"], "ExamGuardianDevelopmentAdmission",
            [new Claim("sub", studentId.ToString()), new Claim("exam_id", examId.ToString()),
             new Claim("purpose", "development-admission"), new Claim("override", Control),
             new Claim("jti", "dev_" + Guid.NewGuid().ToString("N"))],
            expires: DateTime.UtcNow.AddMinutes(2), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
#endif
