using BusinessLogicLayer.Common;
using BusinessLogicLayer.Dtos.Auth;

namespace BusinessLogicLayer.Interfaces;

public interface IAuthService
{
    Task<Result<AuthResponseDto>> LoginAsync(LoginRequestDto request);
    Task<Result<AuthResponseDto>> RefreshTokenAsync(string currentRefreshToken);
}