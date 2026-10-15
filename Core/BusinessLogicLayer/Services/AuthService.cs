using BusinessLogicLayer.Common;
using BusinessLogicLayer.Dtos.Auth;
using BusinessLogicLayer.Interfaces;
using DataAccessLayer.Entities;
using DataAccessLayer.Interfaces;
using DataAccessLayer.Repositories.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace BusinessLogicLayer.Services;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _config;

    public AuthService(IUnitOfWork unitOfWork, IConfiguration config)
    {
        _unitOfWork = unitOfWork;
        _config = config;
    }

    public async Task<Result<AuthResponseDto>> LoginAsync(LoginRequestDto request)
    {
        var userRepo = (IUserRepository)_unitOfWork.Repository<User>();
        var user = await userRepo.GetByLoginAsync(request.Login);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Result.Failure<AuthResponseDto>("Invalid credentials", "INVALID_CREDENTIALS");

        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

        userRepo.Update(user);
        await _unitOfWork.CompleteAsync();

        return Result.Success(new AuthResponseDto
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(accessToken),
            ExpiresAt = accessToken.ValidTo,
            Login = user.Login,
            Role = user.Role,
            RefreshToken = refreshToken
        });
    }

    public async Task<Result<AuthResponseDto>> RefreshTokenAsync(string currentRefreshToken)
    {
        var userRepo = (IUserRepository)_unitOfWork.Repository<User>();
        var user = await userRepo.GetByRefreshTokenAsync(currentRefreshToken);

        if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            return Result.Failure<AuthResponseDto>("Invalid or expired refresh token");

        var newAccess = GenerateAccessToken(user);
        var newRefresh = GenerateRefreshToken();

        user.RefreshToken = newRefresh;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

        userRepo.Update(user);
        await _unitOfWork.CompleteAsync();

        return Result.Success(new AuthResponseDto
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(newAccess),
            ExpiresAt = newAccess.ValidTo,
            Login = user.Login,
            Role = user.Role,
            RefreshToken = newRefresh
        });
    }

    private JwtSecurityToken GenerateAccessToken(User user)
    {
        var key = Encoding.UTF8.GetBytes(_config["Jwt:Key"] ?? throw new InvalidOperationException("Key is missing"));
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Login),
            new Claim("login", user.Login),
            new Claim("id", user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("fullName", user.FullName)
        };

        var creds = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);

        var lifetimeMinutes = double.TryParse(_config["Jwt:LifetimeMinutes"], out var mins) && mins > 0
            ? mins
            : 1440;

        return new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(lifetimeMinutes),
            signingCredentials: creds
        );
    }

    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}