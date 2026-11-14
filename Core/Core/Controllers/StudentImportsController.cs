using BusinessLogicLayer.Constants;
using Core.Importing.Students;
using Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Core.Controllers;
[ApiController, Route("api/students/import"), Authorize(Roles = SystemRoles.Examiner)]
public class StudentImportsController(RosterImportService service) : ControllerBase
{
    [HttpPost("preview"), RequestSizeLimit(500000)]
    public Task<IActionResult> Preview(RosterInput input) => Run(async () => await service.Preview(input, SessionAccess.UserId(User)));
    [HttpPost("commit"), RequestSizeLimit(500000)]
    public Task<IActionResult> Commit(ImportCommit input) => Run(async () => await service.Commit(input.Token, SessionAccess.UserId(User)));
    private async Task<IActionResult> Run(Func<Task<object>> operation) {
        Response.Headers.CacheControl = "no-store";
        try { return Ok(await operation()); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException) { return Conflict(new { message = "Дані змінилися. Оновіть попередній перегляд; уже створені акаунти не дублюються." }); }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "40001") { return Conflict(new { message = "Паралельна зміна списку. Повторіть попередній перегляд." }); }
    }
}
public record ImportCommit(string Token);
