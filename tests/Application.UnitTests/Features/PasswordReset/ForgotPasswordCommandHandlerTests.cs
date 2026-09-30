using Application.DTOs;
using Application.Features.PasswordReset;
using Application.Jobs;
using Application.ServiceInterfaces;
using Application.UnitTests.TestDoubles;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using LibraryApi.Domain.Entities;
using LibraryApi.Domain.RepositoryInterfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.UnitTests.Features.PasswordReset;

public class ForgotPasswordCommandHandlerTests
{
    private const string Code = "VK35oeQ";

    private readonly Mock<IIdentityService> _identity = new();
    private readonly Mock<IPasswordResetCodeRepository> _codes = new();
    private readonly Mock<IVerificationCodeService> _verification = new();
    private readonly Mock<IBackgroundJobClient> _jobs = new();
    private readonly RecordingLogger<ForgotPasswordCommandHandler> _logger = new();

    private static readonly ForgotPasswordCommand Command = new("ada@example.com");

    public ForgotPasswordCommandHandlerTests()
    {
        _verification.Setup(v => v.Generate()).Returns(Code);
        _verification.Setup(v => v.Hash(Code)).Returns("HASHED");
    }

    private ForgotPasswordCommandHandler CreateSut() =>
        new(_identity.Object,
            _codes.Object,
            _verification.Object,
            _jobs.Object,
            _logger);

    private void GivenTheAddressHasAnAccount() =>
        _identity.Setup(s => s.FindByEmailAsync(
                    "ada@example.com", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new PasswordResetTargetDTO("user-1", "ada", "ada@example.com"));

    private void GivenTheAddressHasNoAccount() =>
        _identity.Setup(s => s.FindByEmailAsync(
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((PasswordResetTargetDTO?)null);

    // The endpoint is anonymous, so a different answer for a registered address
    // would make it a free tool for discovering who has an account here.
    [Fact]
    public async Task Succeeds_for_an_address_with_no_account()
    {
        GivenTheAddressHasNoAccount();

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.False(result.IsError);
    }

    [Fact]
    public async Task Writes_nothing_and_sends_nothing_for_an_address_with_no_account()
    {
        GivenTheAddressHasNoAccount();

        await CreateSut().Handle(Command, CancellationToken.None);

        _codes.Verify(repo => repo.AddAsync(
            It.IsAny<PasswordResetCodeModel>(), It.IsAny<CancellationToken>()), Times.Never);
        _jobs.Verify(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Never);
    }

    [Fact]
    public async Task Stores_the_hash_of_the_code_and_never_the_code()
    {
        GivenTheAddressHasAnAccount();

        await CreateSut().Handle(Command, CancellationToken.None);

        _codes.Verify(repo => repo.AddAsync(
            It.Is<PasswordResetCodeModel>(c =>
                c.IdentityUserId == "user-1"
                && c.CodeHash == "HASHED"
                && c.AttemptCount == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Clears_any_earlier_code_before_issuing_a_new_one()
    {
        GivenTheAddressHasAnAccount();

        await CreateSut().Handle(Command, CancellationToken.None);

        _codes.Verify(repo => repo.DeleteAllForUserAsync(
            "user-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Gives_the_code_an_expiry_in_the_future()
    {
        GivenTheAddressHasAnAccount();

        var before = DateTime.UtcNow;
        await CreateSut().Handle(Command, CancellationToken.None);

        _codes.Verify(repo => repo.AddAsync(
            It.Is<PasswordResetCodeModel>(c => c.ExpiresAt > before),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // The plain code has to reach the job, because that is what gets emailed.
    [Fact]
    public async Task Queues_the_email_with_the_plain_code()
    {
        GivenTheAddressHasAnAccount();

        await CreateSut().Handle(Command, CancellationToken.None);

        _jobs.Verify(c => c.Create(
            It.Is<Job>(job =>
                job.Type == typeof(SendPasswordResetEmailJob)
                && job.Method.Name == nameof(SendPasswordResetEmailJob.ExecuteAsync)
                && job.Args.Count == 3
                && (string)job.Args[0] == "ada@example.com"
                && (string)job.Args[1] == "ada"
                && (string)job.Args[2] == Code),
            It.Is<IState>(state => state is EnqueuedState)), Times.Once);
    }

    // Returning an error only when the address is registered would leak exactly
    // what the identical answers above are protecting.
    [Fact]
    public async Task Still_succeeds_when_the_code_cannot_be_stored()
    {
        GivenTheAddressHasAnAccount();
        _codes.Setup(repo => repo.AddAsync(
                  It.IsAny<PasswordResetCodeModel>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("table is missing"));

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Single(_logger.At(LogLevel.Error));
    }

    [Fact]
    public async Task Still_succeeds_when_the_email_cannot_be_queued()
    {
        GivenTheAddressHasAnAccount();
        _jobs.Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()))
             .Throws(new InvalidOperationException("Hangfire schema is missing."));

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Single(_logger.At(LogLevel.Error));
    }

    // A log file outlives the code's fifteen minutes, and on this host it is
    // readable by anyone who can reach the box.
    [Fact]
    public async Task Never_writes_the_code_to_the_log()
    {
        GivenTheAddressHasAnAccount();

        await CreateSut().Handle(Command, CancellationToken.None);

        Assert.False(_logger.Mentions(Code));
    }
}
