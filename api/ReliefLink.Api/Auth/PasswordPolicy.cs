namespace ReliefLink.Api.Auth;

public static class PasswordPolicy
{
    public const int MinLength = 8;

    /// <summary>Returns an error message, or null when the password is acceptable.</summary>
    public static string? Validate(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
            return $"Password must be at least {MinLength} characters.";
        if (!password.Any(char.IsLetter))
            return "Password must contain at least one letter.";
        if (!password.Any(char.IsDigit))
            return "Password must contain at least one digit.";
        return null;
    }
}
