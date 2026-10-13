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

        if (!result.Succeeded)
            return Unauthorized(new { succeeded = false, errors = result.Errors });

        SetRefreshTokenCookie(result.Data.RefreshToken);

        return Ok(new
        {
            succeeded = true,
            response = new { result.Data.AccessToken, result.Data.ExpiresAt, result.Data.Login, result.Data.Role }
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

        if (!result.Succeeded)
            return Unauthorized(new { succeeded = false, errors = result.Errors });

        SetRefreshTokenCookie(result.Data.RefreshToken);

        return Ok(new
        {
            succeeded = true,
            response = new { result.Data.AccessToken, result.Data.ExpiresAt, result.Data.Login, result.Data.Role }
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

    private void SetRefreshTokenCookie(string refreshToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = DateTime.UtcNow.AddDays(7)
        };
        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
    }
}