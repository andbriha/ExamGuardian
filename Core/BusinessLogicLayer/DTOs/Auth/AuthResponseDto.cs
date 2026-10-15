namespace BusinessLogicLayer.Dtos.Auth;

public record AuthResponseDto
{
    public string AccessToken { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public string Login { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
}