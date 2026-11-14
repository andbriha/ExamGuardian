using BusinessLogicLayer.Constants;
using Core.Documents;
using Core.Exports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Core.Controllers;
[ApiController, Route("api/group-results"), Authorize(Roles = SystemRoles.Examiner)]
public class GroupResultsController(GroupResultsService service, GroupPdfWriter writer) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(int examId, string? group, string format = "json") {
        Response.Headers.CacheControl = "no-store";
        if (format is not ("json" or "pdf") || !Request.Query.ContainsKey("group")) return BadRequest();
        try {
            var report = await service.Build(User, examId, group ?? "");
            if (report is null) return NotFound();
            return format == "pdf" ? File(writer.Results(report), "application/pdf", $"group-results-{examId}.pdf") : Ok(report);
        } catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}
