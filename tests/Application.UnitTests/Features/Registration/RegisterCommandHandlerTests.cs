using Application.DTOs;
using Application.Features.Registration;
using Application.Jobs;
using Application.ServiceInterfaces;
using ErrorOr;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using LibraryApi.Application.RepositoryInterfaces;
using LibraryApi.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Application.UnitTests.Features.Registration;

public class RegisterCommandHandlerTests
{
    private readonly Mock<IIdentityService> _identityService = new();
    private readonly Mock<IBackgroundJobClient> _backgroundJobClient = new();
    private readonly Mock<IMembersRepository> _members = new();

    private static readonly RegisterCommand Command =
        new("ada", "Pa55word!", "ada@example.com");

    private static readonly RegisteredUserDTO RegisteredUser =
        new("user-1", "ada", "ada@example.com");

    private global::RegisterCommandHandler CreateSut() =>
        new(_identityService.Object,
            _members.Object,
            _backgroundJobClient.Object,
            NullLogger<global::RegisterCommandHandler>.Instance);

    private void GivenRegistrationReturns(ErrorOr<RegisteredUserDTO> result) =>
        _identityService
            .Setup(s => s.RegisterAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    [Fact]
    public async Task Returns_the_registered_user_when_identity_succeeds()
    {
        GivenRegistrationReturns(RegisteredUser);

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("ada", result.Value.Username);
        Assert.Equal("ada@example.com", result.Value.Email);
    }

    // The whole point of the ErrorOr signature: whatever Identity said about
    // why it refused has to reach the caller. Collapsing it into one opaque
    // failure is what made a broken registration undiagnosable in production.
    [Fact]
    public async Task Passes_every_identity_error_through_untouched()
    {
        GivenRegistrationReturns(new List<Error>
        {
            Error.Validation("Identity.PasswordRequiresDigit", "Passwords must have at least one digit."),
            Error.Validation("Identity.DuplicateUserName", "Username 'ada' is already taken.")
        });

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(2, result.Errors.Count);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("Identity.PasswordRequiresDigit", result.FirstError.Code);
        Assert.Contains(
            result.Errors,
            error => error.Description == "Username 'ada' is already taken.");
    }

    // The handler takes (Username, Password, Email) but IIdentityService takes
    // (username, email, password). Nothing but this test stops a future edit
    // from silently swapping the last two, which would store the password as
    // the email address.
    [Fact]
    public async Task Passes_username_email_and_password_to_identity_in_that_order()
    {
        GivenRegistrationReturns(RegisteredUser);

        await CreateSut().Handle(Command, CancellationToken.None);

        _identityService.Verify(s => s.RegisterAsync(
            "ada",
            "ada@example.com",
            "Pa55word!",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // Enqueuing is the observable behavior here, so asserting on the call is
    // the point rather than an implementation detail. Enqueue<T> is an
    // extension method over IBackgroundJobClient.Create, which is what Moq
    // can see.
    [Fact]
    public async Task Enqueues_the_welcome_email_job_when_registration_succeeds()
    {
        GivenRegistrationReturns(RegisteredUser);

        await CreateSut().Handle(Command, CancellationToken.None);

        _backgroundJobClient.Verify(c => c.Create(
            It.Is<Job>(job =>
                job.Type == typeof(SendWelcomeEmailJob)
                && job.Method.Name == nameof(SendWelcomeEmailJob.ExecuteAsync)
                && job.Args.Count == 2
                && (string)job.Args[0] == "ada@example.com"
                && (string)job.Args[1] == "ada"),
            It.Is<IState>(state => state is EnqueuedState)), Times.Once);
    }

    [Fact]
    public async Task Does_not_enqueue_any_job_when_registration_fails()
    {
        GivenRegistrationReturns(new List<Error>
        {
            Error.Validation("Identity.PasswordTooShort", "Passwords must be at least 6 characters.")
        });

        await CreateSut().Handle(Command, CancellationToken.None);

        _backgroundJobClient.Verify(
            c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()),
            Times.Never);
    }

    // The account exists by the time the job is queued. A Hangfire outage — a
    // missing schema on a fresh database, a SQL user without rights — must not
    // report failure for a registration that actually succeeded, or the caller
    // retries forever against a username they already own.
    [Fact]
    public async Task Still_succeeds_when_the_job_cannot_be_queued()
    {
        GivenRegistrationReturns(RegisteredUser);
        _backgroundJobClient
            .Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Throws(new InvalidOperationException("Hangfire schema is missing."));

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("ada", result.Value.Username);
    }

    [Fact]
    public async Task Creates_a_library_member_for_the_new_account()
    {
        GivenRegistrationReturns(RegisteredUser);

        await CreateSut().Handle(Command, CancellationToken.None);

        _members.Verify(repo => repo.AddMemberAsync(
            It.Is<MemberModel>(member =>
                member.Name == "ada"
                && member.IdentityUserId == "user-1"
                && member.Id == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Does_not_create_a_member_when_registration_fails()
    {
        GivenRegistrationReturns(new List<Error>
        {
            Error.Validation("Identity.DuplicateUserName", "Username 'ada' is already taken.")
        });

        await CreateSut().Handle(Command, CancellationToken.None);

        _members.Verify(repo => repo.AddMemberAsync(
            It.IsAny<MemberModel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Does_not_create_a_second_member_when_the_account_already_has_one()
    {
        GivenRegistrationReturns(RegisteredUser);
        _members
            .Setup(repo => repo.GetMemberByIdentityUserIdAsync(
                "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberModel { Id = 7, Name = "ada", IdentityUserId = "user-1" });

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.False(result.IsError);
        _members.Verify(repo => repo.AddMemberAsync(
            It.IsAny<MemberModel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Still_succeeds_when_the_member_cannot_be_created()
    {
        GivenRegistrationReturns(RegisteredUser);
        _members
            .Setup(repo => repo.AddMemberAsync(
                It.IsAny<MemberModel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Members table is missing."));

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("ada", result.Value.Username);
    }

    [Fact]
    public async Task Still_queues_the_welcome_email_when_the_member_cannot_be_created()
    {
        GivenRegistrationReturns(RegisteredUser);
        _members
            .Setup(repo => repo.AddMemberAsync(
                It.IsAny<MemberModel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Members table is missing."));

        await CreateSut().Handle(Command, CancellationToken.None);

        _backgroundJobClient.Verify(
            c => c.Create(It.IsAny<Job>(), It.Is<IState>(state => state is EnqueuedState)),
            Times.Once);
    }
}
