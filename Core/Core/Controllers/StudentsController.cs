using BusinessLogicLayer.Constants;
using BusinessLogicLayer.DTOs.User;
using BusinessLogicLayer.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StudentsController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IWebHostEnvironment _env;

    public StudentsController(
        IUserService userService,
        IWebHostEnvironment env)
    {
        _userService = userService;
        _env = env;
    }


    [HttpGet]
    [Authorize(Roles = SystemRoles.Examiner)]
    public async Task<IActionResult> GetAll()
    {
        var students = await _userService.GetAllStudentsAsync();
        return Ok(students);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = $"{SystemRoles.Examiner},{SystemRoles.Student}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        if (User.IsInRole(SystemRoles.Student) && Core.Security.SessionAccess.UserId(User) != id) return NotFound();
        var student = await _userService.GetByIdAsync(id);
        return student is null ? NotFound() : Ok(student);
    }

    [HttpPost]
    [Authorize(Roles = SystemRoles.Examiner)]
    public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
    {

        dto.Role = SystemRoles.Student;

        try
        {
            var created = await _userService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById),
                new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    private string GetInferenceImagesPath()
    {
        var envPath = Environment.GetEnvironmentVariable("INFERENCE_IMAGES_PATH");
        if (!string.IsNullOrEmpty(envPath) && Directory.Exists(envPath))
            return envPath;

        var localInference = Path.Combine(_env.ContentRootPath, "..", "..", "Inference", "images");
        if (Directory.Exists(Path.GetFullPath(localInference)))
            return Path.GetFullPath(localInference);

        // Fallback: folder inside ContentRootPath
        var fallback = Path.Combine(_env.ContentRootPath, "InferenceImages");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    [HttpPost("{id:guid}/photo")]
    [Authorize(Roles = SystemRoles.Examiner)]
    public async Task<IActionResult> UploadPhoto(Guid id, IFormFile photo, [FromServices] DataAccessLayer.Data.AppDbContext db)
    {
        if (photo is null || photo.Length == 0)
            return BadRequest(new { message = "Файл порожній" });

        var student = await _userService.GetByIdAsync(id);
        if (student is null) return NotFound();

        if (await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(db.Sessions,s => s.StudentId==id && !s.IsFinished)) return Conflict(new {message="Завершіть активну спробу перед зміною еталона особи."});
        var inferenceImages = GetInferenceImagesPath();
        Directory.CreateDirectory(inferenceImages);

        if (photo.Length > 4 * 1024 * 1024) return BadRequest(new { message = "Photo too large." });
        var bytes = new byte[photo.Length];
        await using (var input = photo.OpenReadStream()) await input.ReadExactlyAsync(bytes);
        if (bytes.Length < 3 || bytes[0] != 0xff || bytes[1] != 0xd8 || bytes[2] != 0xff)
            return BadRequest(new { message = "Upload a JPEG photograph." });
        var filePathById = Path.Combine(inferenceImages, $"{id}.jpg");
        var temporary = filePathById + "." + Guid.NewGuid().ToString("N") + ".upload";
        await System.IO.File.WriteAllBytesAsync(temporary, bytes);
        System.IO.File.Move(temporary, filePathById, true);

        // Зберігаємо відносний шлях у БД
        var photoUrl = $"images/{id}.jpg";
        await _userService.UpdatePhotoAsync(id, photoUrl);

        return Ok(new { photoUrl });
    }

    [HttpGet("{id:guid}/photo")]
    [Authorize(Roles = SystemRoles.Examiner)]
    public async Task<IActionResult> GetPhoto(Guid id)
    {
        var student = await _userService.GetByIdAsync(id);
        if (student is null)
            return NotFound(new { message = "Студента не знайдено" });

        var inferenceImages = GetInferenceImagesPath();
        var filePathById = Path.Combine(inferenceImages, $"{id}.jpg");
        string? targetPath = System.IO.File.Exists(filePathById) ? filePathById : null;

        if (targetPath is null)
            return NotFound(new { message = "Файл фото відсутній" });

        var bytes = await System.IO.File.ReadAllBytesAsync(targetPath);
        return File(bytes, "image/jpeg");
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = SystemRoles.Examiner)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _userService.DeleteAsync(id);
            return NoContent();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException) {
            return Conflict(new { message = "Історія спроб зберігається. Запис із пов'язаними спробами не можна видалити." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}