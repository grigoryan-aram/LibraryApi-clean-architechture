using Application.DTOs;
using Application.Features.Login.Commands;
using Application.ServiceInterfaces;
using Application.UnitTests.TestDoubles;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.UnitTests.Features.Login;

public class LoginCommandHandlerTests
{
    private const string Password = "Pa55word!";

    private readonly Mock<IIdentityService> _identityService = new();
    private readonly RecordingLogger<LoginCommandHandler> _logger = new();

    private static readonly LoginCommand Command = new("ada", Password);

    private LoginCommandHandler CreateSut() =>
        new(_identityService.Object, _logger);

    private void GivenLoginReturns(ErrorOr<LoginResponseDTO> result) =>
        _identityService
            .Setup(s => s.LoginAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    [Fact]
    public async Task Passes_the_credentials_through_and_returns_the_result()
    {
        GivenLoginReturns(new LoginResponseDTO("Signed in."));

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("Signed in.", result.Value.Message);
        _identityService.Verify(s => s.LoginAsync(
            "ada", Password, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Logs_the_username_on_a_successful_sign_in()
    {
        GivenLoginReturns(new LoginResponseDTO("Signed in."));

        await CreateSut().Handle(Command, CancellationToken.None);

        var information = Assert.Single(_logger.At(LogLevel.Information));
        Assert.Contains("ada", information.Message);
    }

    [Fact]
    public async Task Logs_a_failed_attempt_as_a_warning_with_the_error_code()
    {
        GivenLoginReturns(Error.Unauthorized(
            "Login.InvalidCredentials",
            "Invalid username or password."));

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.True(result.IsError);

        var warning = Assert.Single(_logger.At(LogLevel.Warning));
        Assert.Contains("ada", warning.Message);
        Assert.Contains("Login.InvalidCredentials", warning.Message);
    }

    // A mistyped password is one keystroke from a real one, so the failure
    // path is exactly where it must not reach the log in plain text.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Never_writes_the_password_to_the_log(bool signInSucceeds)
    {
        GivenLoginReturns(signInSucceeds
            ? new LoginResponseDTO("Signed in.")
            : Error.Unauthorized("Login.InvalidCredentials", "Invalid."));

        await CreateSut().Handle(Command, CancellationToken.None);

        Assert.NotEmpty(_logger.Entries);
        Assert.False(_logger.Mentions(Password));
    }
}
