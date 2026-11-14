using BusinessLogicLayer.Common;
using BusinessLogicLayer.Dtos.Auth;

namespace BusinessLogicLayer.Interfaces;

public interface IAuthService
{
    Task RevokeAsync(string? refreshToken, Guid? sessionId);
    Task<Result<AuthResponseDto>> LoginAsync(LoginRequestDto request);
    Task<Result<AuthResponseDto>> RefreshTokenAsync(string currentRefreshToken);
}