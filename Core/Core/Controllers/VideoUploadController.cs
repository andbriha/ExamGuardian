// Core/Controllers/VideoUploadController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BusinessLogicLayer.Constants;

namespace Core.Controllers;

[ApiController]
[Route("api/video")]
[Authorize]
public class VideoUploadController : ControllerBase
{
    private readonly string _storagePath;

    public VideoUploadController(IWebHostEnvironment env)
    {
        _storagePath = Path.Combine(env.ContentRootPath, "ExamVideos");
        Directory.CreateDirectory(_storagePath);
    }

    [HttpPost("upload-chunk")]
    [Authorize(Roles = SystemRoles.Student)]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> UploadChunk(
        [FromForm] string sessionId,
        [FromForm] int chunkIndex,
        IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Файл порожній" });

        if (string.IsNullOrWhiteSpace(sessionId))
            return BadRequest(new { message = "sessionId обов'язковий" });

        // Папка для сесії
        var sessionDir = Path.Combine(_storagePath, sessionId);
        Directory.CreateDirectory(sessionDir);

        // chunk_001.mp4, chunk_002.mp4 ...
        var fileName = $"chunk_{chunkIndex:D3}.mp4";
        var filePath = Path.Combine(sessionDir, fileName);

        // CreateNew — не перезаписує якщо файл вже є
        var mode = System.IO.File.Exists(filePath)
            ? FileMode.Create      // перезаписуємо тільки якщо retry
            : FileMode.CreateNew;

        await using var stream = new FileStream(filePath, mode);
        await file.CopyToAsync(stream);

        return Ok(new
        {
            status = "saved",
            sessionId,
            chunkIndex,
            fileName,
            sizeBytes = file.Length,
        });
    }

    // Список усіх відеочанків для сесії
    [HttpGet("{sessionId}/chunks")]
    [AllowAnonymous]
    public IActionResult GetChunks(string sessionId)
    {
        var sessionDir = Path.Combine(_storagePath, sessionId);
        if (!Directory.Exists(sessionDir))
            return Ok(Array.Empty<object>());

        var files = Directory.GetFiles(sessionDir, "chunk_*.mp4")
            .OrderBy(f => f)
            .Select((f, idx) =>
            {
                var fi = new FileInfo(f);
                return new
                {
                    chunkIndex = idx + 1,
                    fileName = fi.Name,
                    sizeBytes = fi.Length,
                    url = $"/api/video/{sessionId}/{idx + 1}"
                };
            })
            .ToList();

        return Ok(files);
    }

    // Екзаменатор / відеоплеєр може завантажити або транслювати чанк
    [HttpGet("{sessionId}/{chunkIndex:int}")]
    [AllowAnonymous]
    public IActionResult GetChunk(string sessionId, int chunkIndex)
    {
        var fileName = $"chunk_{chunkIndex:D3}.mp4";
        var filePath = Path.Combine(_storagePath, sessionId, fileName);

        if (!System.IO.File.Exists(filePath))
            return NotFound();

        return PhysicalFile(filePath, "video/mp4", enableRangeProcessing: true);
    }
}