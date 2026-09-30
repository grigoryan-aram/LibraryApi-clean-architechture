using Application.Features.PasswordReset;
using LibraryApi.Domain.Constants;

namespace Application.UnitTests.Features.PasswordReset;

public class ForgotPasswordCommandValidatorTests
{
    private readonly ForgotPasswordCommandValidator _validator = new();

    [Fact]
    public void Accepts_an_email_address()
    {
        Assert.True(_validator.Validate(new ForgotPasswordCommand("ada@example.com")).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-address")]
    [InlineData("ada@")]
    public void Rejects_anything_that_is_not_an_address(string email)
    {
        var result = _validator.Validate(new ForgotPasswordCommand(email));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            e => e.PropertyName == nameof(ForgotPasswordCommand.Email));
    }
}

public class ResetPasswordCommandValidatorTests
{
    private readonly ResetPasswordCommandValidator _validator = new();

    private static ResetPasswordCommand Valid =>
        new("ada@example.com", "VK35oeQ", "N3w-Pa55word!");

    [Fact]
    public void Accepts_a_complete_command()
    {
        Assert.True(_validator.Validate(Valid).IsValid);
    }

    [Fact]
    public void The_sample_code_is_the_configured_length()
    {
        Assert.Equal(PasswordResetRules.CodeLength, "VK35oeQ".Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("VK35oe")]
    [InlineData("VK35oeQQ")]
    public void Rejects_a_code_of_the_wrong_length(string code)
    {
        var result = _validator.Validate(Valid with { Code = code });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            e => e.PropertyName == nameof(ResetPasswordCommand.Code));
    }

    [Fact]
    public void Rejects_an_empty_new_password()
    {
        var result = _validator.Validate(Valid with { NewPassword = "" });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            e => e.PropertyName == nameof(ResetPasswordCommand.NewPassword));
    }

    // Strength is Identity's call, so a short password has to get past the
    // validator and be refused with Identity's own message instead.
    [Fact]
    public void Leaves_password_strength_to_identity()
    {
        Assert.True(_validator.Validate(Valid with { NewPassword = "a" }).IsValid);
    }

    [Fact]
    public void Rejects_an_address_that_is_not_an_address()
    {
        var result = _validator.Validate(Valid with { Email = "nope" });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            e => e.PropertyName == nameof(ResetPasswordCommand.Email));
    }
}
