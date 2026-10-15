// BusinessLogicLayer/Services/UserService.cs
using BCrypt.Net;
using BusinessLogicLayer.Constants;
using BusinessLogicLayer.DTOs.User;
using BusinessLogicLayer.Interfaces;
using DataAccessLayer.Entities;
using DataAccessLayer.Interfaces;
using DataAccessLayer.Repositories.Interfaces;

namespace BusinessLogicLayer.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _users;

    public UserService(IUserRepository users)
    {
        _users = users;
    }

    public async Task<UserDto?> GetByIdAsync(Guid id)
    {
        var user = await _users.GetByIdAsync(id);
        return user is null ? null : Map(user);
    }

    public async Task<IEnumerable<UserDto>> GetAllStudentsAsync()
    {
        var all = await _users.GetAllAsync();
        return all
            .Where(u => u.Role == SystemRoles.Student)
            .Select(Map);
    }

    public async Task<UserDto> CreateAsync(CreateUserDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName) || dto.FullName.Length > 150 || string.IsNullOrWhiteSpace(dto.Login) || !System.Text.RegularExpressions.Regex.IsMatch(dto.Login ?? "", @"^[a-zA-Z0-9._-]{3,100}$") || string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 12)
            throw new InvalidOperationException("Name, a safe login and password of at least 12 characters are required.");
        var existing = await _users.GetByLoginAsync(dto.Login!);
        if (existing is not null)
            throw new InvalidOperationException(
                $"Логін '{dto.Login}' вже зайнятий");

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = dto.FullName.Trim(),
            Login = dto.Login!,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = dto.Role,
            Group = string.IsNullOrWhiteSpace(dto.Group) ? "Основна група" : dto.Group.Trim(),
        };

        await _users.AddAsync(user);
        await _users.SaveChangesAsync();
        return Map(user);
    }

    public async Task<UserDto> UpdatePhotoAsync(Guid id, string photoUrl)
    {
        var user = await _users.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Користувача {id} не знайдено");

        user.PhotoUrl = photoUrl;
        _users.Update(user);
        await _users.SaveChangesAsync();
        return Map(user);
    }

    public async Task DeleteAsync(Guid id)
    {
        var user = await _users.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Користувача {id} не знайдено");

        _users.Delete(user);
        await _users.SaveChangesAsync();
    }

    private static UserDto Map(User u) => new()
    {
        Id = u.Id,
        FullName = u.FullName,
        Login = u.Login,
        Role = u.Role,
        Group = u.Group ?? "Основна група",
        PhotoUrl = u.PhotoUrl,
    };
}