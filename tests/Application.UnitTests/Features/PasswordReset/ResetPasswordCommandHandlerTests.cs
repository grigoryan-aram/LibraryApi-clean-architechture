using Application.DTOs;
using Application.Features.PasswordReset;
using Application.ServiceInterfaces;
using Application.UnitTests.TestDoubles;
using ErrorOr;
using LibraryApi.Domain.Constants;
using LibraryApi.Domain.Entities;
using LibraryApi.Domain.RepositoryInterfaces;
using Moq;

namespace Application.UnitTests.Features.PasswordReset;

public class ResetPasswordCommandHandlerTests
{
    private const string Code = "VK35oeQ";

    private readonly Mock<IIdentityService> _identity = new();
    private readonly Mock<IPasswordResetCodeRepository> _codes = new();
    private readonly Mock<IVerificationCodeService> _verification = new();
    private readonly RecordingLogger<ResetPasswordCommandHandler> _logger = new();

    private static readonly ResetPasswordCommand Command =
        new("ada@example.com", Code, "N3w-Pa55word!");

    private ResetPasswordCommandHandler CreateSut() =>
        new(_identity.Object, _codes.Object, _verification.Object, _logger);

    private void GivenTheAddressHasAnAccount() =>
        _identity.Setup(s => s.FindByEmailAsync(
                    "ada@example.com", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new PasswordResetTargetDTO("user-1", "ada", "ada@example.com"));

    private void GivenAStoredCode(int attemptCount = 0) =>
        _codes.Setup(repo => repo.GetActiveAsync(
                  "user-1", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new PasswordResetCodeModel
              {
                  Id = 1,
                  IdentityUserId = "user-1",
                  CodeHash = "HASHED",
                  AttemptCount = attemptCount,
                  ExpiresAt = DateTime.UtcNow.AddMinutes(10)
              });

    private void GivenNoStoredCode() =>
        _codes.Setup(repo => repo.GetActiveAsync(
                  It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((PasswordResetCodeModel?)null);

    private void GivenTheCodeMatches(bool matches) =>
        _verification.Setup(v => v.Verify(Code, "HASHED")).Returns(matches);

    private void GivenIdentityAcceptsTheNewPassword() =>
        _identity.Setup(s => s.ResetPasswordAsync(
                    "user-1", It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result.Success);

    [Fact]
    public async Task Refuses_an_address_with_no_account()
    {
        _identity.Setup(s => s.FindByEmailAsync(
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((PasswordResetTargetDTO?)null);

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("PasswordReset.InvalidCode", result.FirstError.Code);
        _identity.Verify(s => s.ResetPasswordAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Refuses_when_no_code_is_outstanding()
    {
        GivenTheAddressHasAnAccount();
        GivenNoStoredCode();

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("PasswordReset.InvalidCode", result.FirstError.Code);
    }

    [Fact]
    public async Task Refuses_a_wrong_code_and_counts_the_attempt()
    {
        GivenTheAddressHasAnAccount();
        GivenAStoredCode();
        GivenTheCodeMatches(false);

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("PasswordReset.InvalidCode", result.FirstError.Code);
        _codes.Verify(repo => repo.UpdateAsync(
            It.Is<PasswordResetCodeModel>(c => c.AttemptCount == 1),
            It.IsAny<CancellationToken>()), Times.Once);
        _identity.Verify(s => s.ResetPasswordAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // The attempt cap, not the request rate limiter, is what puts a guessing
    // attack out of reach. Once it is reached the code is dead even if the
    // right one turns up.
    [Fact]
    public async Task Refuses_a_spent_code_without_even_checking_it()
    {
        GivenTheAddressHasAnAccount();
        GivenAStoredCode(attemptCount: PasswordResetRules.MaxAttempts);
        GivenTheCodeMatches(true);

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("PasswordReset.InvalidCode", result.FirstError.Code);
        _verification.Verify(v => v.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _identity.Verify(s => s.ResetPasswordAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Changes_the_password_when_the_code_matches()
    {
        GivenTheAddressHasAnAccount();
        GivenAStoredCode();
        GivenTheCodeMatches(true);
        GivenIdentityAcceptsTheNewPassword();

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.False(result.IsError);
        _identity.Verify(s => s.ResetPasswordAsync(
            "user-1", "N3w-Pa55word!", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Burns_the_code_once_it_has_been_used()
    {
        GivenTheAddressHasAnAccount();
        GivenAStoredCode();
        GivenTheCodeMatches(true);
        GivenIdentityAcceptsTheNewPassword();

        await CreateSut().Handle(Command, CancellationToken.None);

        _codes.Verify(repo => repo.DeleteAllForUserAsync(
            "user-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    // Identity owns password strength, and its messages are the only thing that
    // explains a refusal, so they have to reach the caller intact.
    [Fact]
    public async Task Passes_identity_password_complaints_through()
    {
        GivenTheAddressHasAnAccount();
        GivenAStoredCode();
        GivenTheCodeMatches(true);
        _identity.Setup(s => s.ResetPasswordAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Error>
                 {
                     Error.Validation(
                         "Identity.PasswordTooShort",
                         "Passwords must be at least 6 characters.")
                 });

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Identity.PasswordTooShort", result.FirstError.Code);
    }

    // A rejected password is the caller's own mistake, not a spent code, so
    // they get to try again rather than starting the whole flow over.
    [Fact]
    public async Task Keeps_the_code_usable_when_identity_rejects_the_password()
    {
        GivenTheAddressHasAnAccount();
        GivenAStoredCode();
        GivenTheCodeMatches(true);
        _identity.Setup(s => s.ResetPasswordAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Error>
                 {
                     Error.Validation("Identity.PasswordTooShort", "Too short.")
                 });

        await CreateSut().Handle(Command, CancellationToken.None);

        _codes.Verify(repo => repo.DeleteAllForUserAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Unknown address, no code, wrong code and spent code must be
    // indistinguishable from outside, or the differences map out which
    // addresses have accounts.
    [Fact]
    public async Task Answers_identically_however_the_code_fails()
    {
        var descriptions = new List<string>();

        _identity.Setup(s => s.FindByEmailAsync(
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((PasswordResetTargetDTO?)null);
        descriptions.Add((await CreateSut().Handle(Command, CancellationToken.None))
            .FirstError.Description);

        GivenTheAddressHasAnAccount();
        GivenNoStoredCode();
        descriptions.Add((await CreateSut().Handle(Command, CancellationToken.None))
            .FirstError.Description);

        GivenAStoredCode();
        GivenTheCodeMatches(false);
        descriptions.Add((await CreateSut().Handle(Command, CancellationToken.None))
            .FirstError.Description);

        GivenAStoredCode(attemptCount: PasswordResetRules.MaxAttempts);
        descriptions.Add((await CreateSut().Handle(Command, CancellationToken.None))
            .FirstError.Description);

        Assert.Single(descriptions.Distinct());
    }

    [Fact]
    public async Task Never_writes_the_code_or_the_new_password_to_the_log()
    {
        GivenTheAddressHasAnAccount();
        GivenAStoredCode();
        GivenTheCodeMatches(true);
        GivenIdentityAcceptsTheNewPassword();

        await CreateSut().Handle(Command, CancellationToken.None);

        Assert.False(_logger.Mentions(Code));
        Assert.False(_logger.Mentions("N3w-Pa55word!"));
    }
}
