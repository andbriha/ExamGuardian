using BusinessLogicLayer.Constants;
using Core.Security;
using Core.Exports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Core.Controllers;

[ApiController, Authorize(Roles = SystemRoles.Examiner)]
public class GradeExportsController(SessionAccess access, GradeExportService exports) : ControllerBase
{
    [HttpGet("api/grade-exports")]
    public async Task<IActionResult> Catalog()
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await exports.Catalog(User));
    }
    [HttpGet("api/exams/{examId:int}/grades")]
    public async Task<IActionResult> Export(int examId, [FromQuery] string? group)
    {
        if (!await access.OwnsExamAsync(User, examId)) return NotFound();
        if (!Request.Query.ContainsKey("group")) return BadRequest(new { message = "Оберіть групу для експорту." });
        group ??= "";
        var latest = (await exports.Latest(User, examId)).Where(r => r.Group == group).ToList();
        var rows = latest.Where(r => GradeExportService.BlockReason(r) is null).ToList();
        if (rows.Count == 0) return Conflict(new { message = latest.Count == 0 ? "У цій групі немає спроб цього іспиту." :
            "Оцінки недоступні: " + string.Join("; ", latest.Select(GradeExportService.BlockReason).Distinct().Select(c => GradeExportService.ReasonText(c!))) });
        Response.Headers.CacheControl = "no-store";
        return File(GradeExportService.Csv(rows), "text/csv; charset=utf-8", $"exam-{examId}-grades.csv");
    }
}
