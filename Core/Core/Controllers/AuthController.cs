using Microsoft.AspNetCore.RateLimiting;
using BusinessLogicLayer.Dtos.Auth;
using BusinessLogicLayer.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebAPI.Controllers;

[ApiController]
[EnableRateLimiting("authentication")]
[RequestSizeLimit(8192)]
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
        if (!SameOriginRequest()) return StatusCode(403);
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
        if (!SameOriginRequest()) return StatusCode(403);
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

    [AllowAnonymous, HttpPost("logout")]
    public async Task<IActionResult> Logout() {
        if (!SameOriginRequest()) return StatusCode(403);
        Guid? session = Guid.TryParse(User.FindFirst("sid")?.Value, out var id) ? id : null;
        await _authService.RevokeAsync(Request.Cookies["refreshToken"], session);
        Response.Cookies.Delete("refreshToken", new CookieOptions { Path = "/api/Auth", Secure = !HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment(), HttpOnly = true, SameSite = SameSiteMode.Strict });
        return NoContent();
    }
    private bool SameOriginRequest() {
        if (Request.Headers["Sec-Fetch-Site"] == "cross-site") return false;
        var origin = Request.Headers.Origin.ToString();
        return origin.Length == 0 || (Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
            uri.Authority.Equals(Request.Host.Value, StringComparison.OrdinalIgnoreCase) && uri.Scheme == Request.Scheme);
    }

    private void SetRefreshTokenCookie(string refreshToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps || !HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = "/api/Auth",
            Expires = DateTime.UtcNow.AddDays(7)
        };
        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
    }
}