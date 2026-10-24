using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Core.Controllers;

[ApiController, Route("api/system"), Authorize]
public class SystemController(IConfiguration config,IWebHostEnvironment env) : ControllerBase
{
    [HttpGet("capabilities")]
    public IActionResult Capabilities()
    {
        _ = config;
        const string flavor="shared";
        var enabled=false;
        var allowed=Array.Empty<string>();
#if EXAMGUARDIAN_DEVELOPMENT
        enabled = env.IsDevelopment() && config.GetValue<bool>("Development:AllowIdentityBypass");
        allowed = enabled ? [Core.Development.DevelopmentAdmission.Control] : [];
#endif
        return Ok(new {buildId=typeof(SystemController).Assembly.GetName().Version?.ToString(),environment=env.EnvironmentName,buildFlavor=flavor,protocolVersion=4,developmentToolsAvailable=enabled,allowedOverrides=allowed});
    }
}
