namespace ReliefLink.Api.Domain;

public enum UserRole
{
    Citizen,
    Volunteer,
    Coordinator,
    Admin
}

public class User : BaseEntity
{
    public string FullName { get; set; } = string.Empty;

    /// <summary>Stored lower-case and trimmed; unique.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>BCrypt hash. The plain password is never stored or logged.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Citizen;
    public string? Phone { get; set; }
    public string? District { get; set; }
    public bool IsActive { get; set; } = true;
}
