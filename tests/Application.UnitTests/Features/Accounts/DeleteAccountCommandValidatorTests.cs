using Application.Features.Accounts;

namespace Application.UnitTests.Features.Accounts;

public class DeleteAccountCommandValidatorTests
{
    private readonly DeleteAccountCommandValidator _validator = new();

    [Fact]
    public void Accepts_a_signed_in_caller_with_a_password() =>
        Assert.True(_validator.Validate(new DeleteAccountCommand("user-1", "Pa55word!")).IsValid);

    // An empty id means the claim was missing — never something to act on.
    [Fact]
    public void Rejects_a_missing_user_id() =>
        Assert.False(_validator.Validate(new DeleteAccountCommand("", "Pa55word!")).IsValid);

    [Fact]
    public void Rejects_a_missing_password()
    {
        var result = _validator.Validate(new DeleteAccountCommand("user-1", ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(DeleteAccountCommand.Password));
    }
}
