using BusinessLogicLayer.Constants;
using Core.Exports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Core.Controllers;

[ApiController]
[Route("api/groups/credentials")]
[Authorize(Roles = SystemRoles.Examiner)]
public class GroupCredentialsController(GroupCredentialsService service) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> Logins([FromQuery] string group, string format = "csv") => Export(group, null, format);

    [HttpPost]
    public Task<IActionResult> Issue(IssueGroupCredentials request, string format = "csv") => Export(request.Group, request.StudentIds ?? [], format);

    private async Task<IActionResult> Export(string group, Guid[]? students, string format)
    {
        Response.Headers.CacheControl = "no-store";
        try { return File(await service.Export(Core.Security.SessionAccess.UserId(User), group, students, format), format == "pdf" ? "application/pdf" : "text/csv; charset=utf-8", format == "pdf" ? "group-credentials.pdf" : students is null ? "group-logins.csv" : "group-credentials.csv"); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException) { return Conflict(new { message = "Склад групи змінився. Оновіть список перед повторною спробою." }); }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "40001") { return Conflict(new { message = "Дані змінилися під час випуску. Оновіть групу і повторіть дію." }); }
    }
}

public record IssueGroupCredentials(string Group, Guid[]? StudentIds);
