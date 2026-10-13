using System.ComponentModel.DataAnnotations;
using ReliefLink.Api.Domain;

namespace ReliefLink.Api.Controllers.Dtos;

/// <summary>Roles a person may choose for themselves. Staff roles are created by an administrator only.</summary>
public enum SelfRegisterRole
{
    Citizen,
    Volunteer
}

public record RegisterRequest(
    [Required, StringLength(120, MinimumLength = 2)] string FullName,
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(100)] string Password,
    [Phone, StringLength(30)] string? Phone,
    [StringLength(60)] string? District,
    SelfRegisterRole Role = SelfRegisterRole.Citizen);

public record LoginRequest(
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(100)] string Password);

public record UserDto(
    Guid Id,
    string FullName,
    string Email,
    string Role,
    string? Phone,
    string? District,
    bool IsActive,
    DateTime CreatedAt)
{
    public static UserDto From(User u) =>
        new(u.Id, u.FullName, u.Email, u.Role.ToString(), u.Phone, u.District, u.IsActive, u.CreatedAt);
}

public record AuthResponse(string Token, DateTime ExpiresAtUtc, UserDto User);

public record CreateUserRequest(
    [Required, StringLength(120, MinimumLength = 2)] string FullName,
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(100)] string Password,
    [Phone, StringLength(30)] string? Phone,
    [StringLength(60)] string? District,
    UserRole Role);

public record SetActiveRequest(bool IsActive);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
