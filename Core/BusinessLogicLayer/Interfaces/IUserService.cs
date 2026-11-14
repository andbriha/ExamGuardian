using BusinessLogicLayer.DTOs.User;

namespace BusinessLogicLayer.Interfaces;

public interface IUserService
{
    Task<UserDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<UserDto>> GetAllStudentsAsync();
    Task<UserDto> CreateAsync(CreateUserDto dto);
    Task<UserDto> UpdatePhotoAsync(Guid id, string photoUrl);
    Task DeleteAsync(Guid id);
}