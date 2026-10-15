using BusinessLogicLayer.Dtos.Auth;
using BusinessLogicLayer.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        var result = await _authService.LoginAsync(dto);

        if (!result.Succeeded || result.Data is null)
            return Unauthorized(new { succeeded = false, errors = result.Errors });

        SetRefreshTokenCookie(result.Data.RefreshToken);

        return Ok(new
        {
            succeeded = true,
            response = new { result.Data.AccessToken, result.Data.ExpiresAt, result.Data.Login, result.Data.Role, ProtocolVersion = 4 }
        });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh()
    {
        var refreshToken = Request.Cookies["refreshToken"];

        if (string.IsNullOrEmpty(refreshToken))
            return BadRequest(new { succeeded = false, errors = new[] { "Refresh token is missing" } });

        var result = await _authService.RefreshTokenAsync(refreshToken);

        if (!result.Succeeded || result.Data is null)
            return Unauthorized(new { succeeded = false, errors = result.Errors });

        SetRefreshTokenCookie(result.Data.RefreshToken);

        return Ok(new
        {
            succeeded = true,
            response = new { result.Data.AccessToken, result.Data.ExpiresAt, result.Data.Login, result.Data.Role, ProtocolVersion = 4 }
        });
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var login = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;

        return Ok(new { Login = login, Role = role });
    }

    [Authorize, HttpPost("logout")]
    public async Task<IActionResult> Logout([FromServices] DataAccessLayer.Data.AppDbContext db)
    {
        var user = await db.Users.FindAsync(Core.Security.SessionAccess.UserId(User));
        if (user is not null) { user.RefreshToken = null; user.RefreshTokenExpiryTime = null; await db.SaveChangesAsync(); }
        Response.Cookies.Delete("refreshToken"); return NoContent();
    }

    private void SetRefreshTokenCookie(string refreshToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps || !HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Expires = DateTime.UtcNow.AddDays(7)
        };
        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
    }
}