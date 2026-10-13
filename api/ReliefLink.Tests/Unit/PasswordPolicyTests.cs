using ReliefLink.Api.Auth;

namespace ReliefLink.Tests.Unit;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("Abcdef12")]
    [InlineData("correct horse 9")]
    [InlineData("P@ssw0rd!")]
    public void Acceptable_Passwords_PassValidation(string password)
    {
        Assert.Null(PasswordPolicy.Validate(password));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Ab1")]
    [InlineData("abcdef1")]
    public void TooShort_ReturnsLengthError(string? password)
    {
        Assert.Contains("at least 8", PasswordPolicy.Validate(password));
    }

    [Fact]
    public void ExactlyMinimumLength_IsAccepted() =>
        Assert.Null(PasswordPolicy.Validate("abcdefg1"));

    [Fact]
    public void NoDigit_IsRejected() =>
        Assert.Contains("digit", PasswordPolicy.Validate("OnlyLettersHere"));

    [Fact]
    public void NoLetter_IsRejected() =>
        Assert.Contains("letter", PasswordPolicy.Validate("12345678!!"));
}
