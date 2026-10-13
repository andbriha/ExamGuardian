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
    public async Task<IActionResult> UploadPhoto(Guid id, IFormFile photo)
    {
        if (photo is null || photo.Length == 0)
            return BadRequest(new { message = "Файл порожній" });

        var student = await _userService.GetByIdAsync(id);
        if (student is null) return NotFound();

        var inferenceImages = GetInferenceImagesPath();
        Directory.CreateDirectory(inferenceImages);

        // Зберігаємо і по GUID і по Login, щоб мікросервіс Inference знаходив у будь-якому випадку
        var filePathById = Path.Combine(inferenceImages, $"{id}.jpg");
        var filePathByLogin = Path.Combine(inferenceImages, $"{student.Login}.jpg");

        await using (var stream = System.IO.File.Create(filePathById))
        {
            await photo.CopyToAsync(stream);
        }

        if (!string.IsNullOrWhiteSpace(student.Login))
        {
            try
            {
                System.IO.File.Copy(filePathById, filePathByLogin, true);
            }
            catch
            {
                // ignore
            }
        }

        // Зберігаємо відносний шлях у БД
        var photoUrl = $"images/{id}.jpg";
        await _userService.UpdatePhotoAsync(id, photoUrl);

        return Ok(new { photoUrl });
    }

    [HttpGet("{id:guid}/photo")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPhoto(Guid id)
    {
        var student = await _userService.GetByIdAsync(id);
        if (student is null)
            return NotFound(new { message = "Студента не знайдено" });

        var inferenceImages = GetInferenceImagesPath();
        var filePathById = Path.Combine(inferenceImages, $"{id}.jpg");
        var filePathByLogin = Path.Combine(inferenceImages, $"{student.Login}.jpg");

        string? targetPath = null;
        if (System.IO.File.Exists(filePathById))
            targetPath = filePathById;
        else if (System.IO.File.Exists(filePathByLogin))
            targetPath = filePathByLogin;

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
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}