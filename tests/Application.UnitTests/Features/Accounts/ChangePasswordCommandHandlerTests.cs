using Application.Features.Accounts;
using Application.ServiceInterfaces;
using Application.UnitTests.TestDoubles;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.UnitTests.Features.Accounts;

public class ChangePasswordCommandHandlerTests
{
    private readonly Mock<IIdentityService> _identity = new();
    private readonly RecordingLogger<ChangePasswordCommandHandler> _logger = new();

    private static readonly ChangePasswordCommand Command =
        new("user-1", "Old-Pa55word!", "New-Pa55word!");

    private ChangePasswordCommandHandler CreateSut() => new(_identity.Object, _logger);

    private void GivenChangeReturns(ErrorOr<Success> result) =>
        _identity.Setup(s => s.ChangePasswordAsync(
                     It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(result);

    [Fact]
    public async Task Passes_the_caller_and_both_passwords_through_in_order()
    {
        GivenChangeReturns(Result.Success);

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.False(result.IsError);
        _identity.Verify(s => s.ChangePasswordAsync(
            "user-1", "Old-Pa55word!", "New-Pa55word!", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Returns_a_wrong_current_password_as_unauthorized()
    {
        GivenChangeReturns(Error.Unauthorized("Account.InvalidPassword", "That password is not correct."));

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.Equal(ErrorType.Unauthorized, result.FirstError.Type);
        Assert.Equal("Account.InvalidPassword", result.FirstError.Code);
    }

    // Identity can reject the new password for several reasons at once; the
    // caller needs all of them, not the first.
    [Fact]
    public async Task Passes_every_strength_complaint_through()
    {
        GivenChangeReturns(new List<Error>
        {
            Error.Validation("Identity.PasswordRequiresDigit", "Passwords must have at least one digit."),
            Error.Validation("Identity.PasswordTooShort", "Passwords must be at least 6 characters.")
        });

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public async Task Never_logs_either_password()
    {
        GivenChangeReturns(Error.Unauthorized("Account.InvalidPassword", "That password is not correct."));
        await CreateSut().Handle(Command, CancellationToken.None);

        GivenChangeReturns(Result.Success);
        await CreateSut().Handle(Command, CancellationToken.None);

        Assert.NotEmpty(_logger.Entries);
        Assert.DoesNotContain(_logger.Entries, entry =>
            entry.Message.Contains("Old-Pa55word!") || entry.Message.Contains("New-Pa55word!"));
    }
}
