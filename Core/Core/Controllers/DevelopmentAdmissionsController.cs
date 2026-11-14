#if EXAMGUARDIAN_DEVELOPMENT
using BusinessLogicLayer.Constants;
using Core.Development;
using Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Core.Controllers;

[ApiController, Route("api/development/admissions"), Authorize(Roles = SystemRoles.Student)]
public class DevelopmentAdmissionsController(DevelopmentAdmission policy, SessionAccess access) : ControllerBase
{
    public record RequestAdmission(int ExamId, string[] Overrides);
    [HttpPost]
    public async Task<IActionResult> Issue(RequestAdmission request)
    {
        if (!policy.Enabled) return NotFound();
        if (request.Overrides is not [DevelopmentAdmission.Control])
            return BadRequest(new { message = "Only the biometricAdmission override is supported." });
        if (!(await access.AvailableExamsAsync(User)).Contains(request.ExamId)) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return Ok(new { admissionToken = policy.Issue(SessionAccess.UserId(User), request.ExamId), identityVerificationSkipped = true });
    }
}
#endif
