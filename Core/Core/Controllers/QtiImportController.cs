using Core.Importing.Qti;
using BusinessLogicLayer.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Core.Controllers;

[ApiController, Route("api/exams/import-qti"), Authorize(Roles = SystemRoles.Examiner)]
public class QtiImportController(QtiPackageReader reader) : ControllerBase
{
    private const int Limit = 10 * 1024 * 1024;
    [HttpPost, RequestSizeLimit(Limit + 65536), RequestFormLimits(MultipartBodyLengthLimit = Limit + 65536)]
    public async Task<IActionResult> Preview(IFormFile file)
    {
        if (file.Length is 0 or > Limit) return BadRequest(new { message = "Оберіть XML або ZIP до 10 МБ." });
        using var input = new MemoryStream();
        await file.CopyToAsync(input);
        try { return Ok(reader.Read(input, file.FileName)); }
        catch (InvalidDataException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
