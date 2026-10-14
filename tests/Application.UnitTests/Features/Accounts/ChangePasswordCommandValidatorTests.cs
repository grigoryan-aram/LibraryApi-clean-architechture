using Application.Features.Accounts;

namespace Application.UnitTests.Features.Accounts;

public class ChangePasswordCommandValidatorTests
{
    private readonly ChangePasswordCommandValidator _validator = new();

    [Fact]
    public void Accepts_a_signed_in_caller_with_both_passwords() =>
        Assert.True(_validator.Validate(
            new ChangePasswordCommand("user-1", "Old-Pa55word!", "New-Pa55word!")).IsValid);

    [Theory]
    [InlineData("", "Old-Pa55word!", "New-Pa55word!", nameof(ChangePasswordCommand.IdentityUserId))]
    [InlineData("user-1", "", "New-Pa55word!", nameof(ChangePasswordCommand.CurrentPassword))]
    [InlineData("user-1", "Old-Pa55word!", "", nameof(ChangePasswordCommand.NewPassword))]
    public void Rejects_anything_missing(string userId, string current, string next, string property)
    {
        var result = _validator.Validate(new ChangePasswordCommand(userId, current, next));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == property);
    }

    // Would succeed in Identity, rotate the stamp, and sign out every other
    // session for no change at all.
    [Fact]
    public void Rejects_a_new_password_identical_to_the_current_one()
    {
        var result = _validator.Validate(
            new ChangePasswordCommand("user-1", "Same-Pa55word!", "Same-Pa55word!"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ChangePasswordCommand.NewPassword));
    }
}
