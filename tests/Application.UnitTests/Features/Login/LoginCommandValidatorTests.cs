using Application.Features.Login.Commands;

namespace Application.UnitTests.Features.Login;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    private static LoginCommand Valid => new("ada", "Pa55word!");

    [Fact]
    public void Accepts_a_username_and_password()
    {
        Assert.True(_validator.Validate(Valid).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_missing_username(string username)
    {
        var result = _validator.Validate(Valid with { Username = username });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginCommand.Username));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_missing_password(string password)
    {
        var result = _validator.Validate(Valid with { Password = password });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginCommand.Password));
    }

    // Sign-in judges whether the credentials match, nothing else. A minimum
    // length here locked out any account whose password predates a policy
    // change, and answered a short password with "must be at least 6
    // characters" instead of the same refusal every other bad credential gets.
    [Theory]
    [InlineData("a")]
    [InlineData("short")]
    public void Does_not_judge_password_length(string password)
    {
        Assert.True(_validator.Validate(Valid with { Password = password }).IsValid);
    }
}
