using Application.Features.Accounts;
using Application.ServiceInterfaces;
using Application.UnitTests.TestDoubles;
using ErrorOr;
using LibraryApi.Domain.Constants;
using LibraryApi.Domain.Entities;
using LibraryApi.Domain.RepositoryInterfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Application.UnitTests.Features.Accounts;

public class DeleteAccountCommandHandlerTests
{
    private readonly Mock<IIdentityService> _identity = new();
    private readonly Mock<IMembersRepository> _members = new();
    private readonly Mock<ILoansRepository> _loans = new();
    private readonly Mock<IPasswordResetCodeRepository> _resetCodes = new();
    private readonly RecordingUnitOfWork _unitOfWork = new();

    private static readonly DeleteAccountCommand Command = new("user-1", "Pa55word!");

    public DeleteAccountCommandHandlerTests()
    {
        _identity.Setup(s => s.CheckPasswordAsync("user-1", "Pa55word!", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result.Success);
        _identity.Setup(s => s.DeleteAccountAsync("user-1", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result.Deleted);
        _loans.Setup(r => r.GetLoansForMemberAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync([]);
    }

    private DeleteAccountCommandHandler CreateSut() =>
        new(_identity.Object,
            _members.Object,
            _loans.Object,
            _resetCodes.Object,
            _unitOfWork,
            NullLogger<DeleteAccountCommandHandler>.Instance);

    private void GivenLinkedMember() =>
        _members.Setup(r => r.GetMemberByIdentityUserIdAsync("user-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MemberModel { Id = 7, Name = "ada", IdentityUserId = "user-1" });

    private void GivenLoans(params DateTime?[] returnedAt) =>
        _loans.Setup(r => r.GetLoansForMemberAsync(7, It.IsAny<CancellationToken>()))
              .ReturnsAsync(returnedAt
                  .Select((returned, i) => new LoanModel { Id = i + 1, MemberId = 7, ReturnedAt = returned })
                  .ToList());

    private void VerifyNothingDeleted()
    {
        _identity.Verify(s => s.DeleteAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _members.Verify(r => r.UpdateMemberAsync(It.IsAny<MemberModel>(), It.IsAny<CancellationToken>()), Times.Never);
        _identity.Verify(s => s.SignOutAsync(), Times.Never);
    }

    [Fact]
    public async Task Refuses_a_wrong_password_without_deleting_anything()
    {
        _identity.Setup(s => s.CheckPasswordAsync("user-1", "wrong", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Error.Unauthorized("Account.InvalidPassword", "That password is not correct."));

        var result = await CreateSut().Handle(Command with { Password = "wrong" }, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Unauthorized, result.FirstError.Type);
        Assert.Equal("Account.InvalidPassword", result.FirstError.Code);
        VerifyNothingDeleted();
    }

    // The seeder would recreate it on the next start, so it would only look
    // deleted.
    [Fact]
    public async Task Refuses_to_delete_an_administrator()
    {
        _identity.Setup(s => s.IsInRoleAsync("user-1", Roles.Admin, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.Equal(ErrorType.Conflict, result.FirstError.Type);
        Assert.Equal("Account.AdminCannotBeDeleted", result.FirstError.Code);
        VerifyNothingDeleted();
    }

    [Fact]
    public async Task Refuses_while_books_are_still_on_loan_and_says_how_many()
    {
        GivenLinkedMember();
        GivenLoans(null, null, DateTime.UtcNow.AddDays(-3));

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.Equal(ErrorType.Conflict, result.FirstError.Type);
        Assert.Equal("Account.HasOpenLoans", result.FirstError.Code);
        Assert.Contains("2 books", result.FirstError.Description);
        VerifyNothingDeleted();
    }

    [Fact]
    public async Task Keeps_the_member_and_its_history_but_unlinks_and_anonymises_it()
    {
        GivenLinkedMember();
        GivenLoans(DateTime.UtcNow.AddDays(-3));

        // Recorded inside the stub: Moq keeps the argument by reference, so a
        // matcher run at Verify time would read whatever it became later.
        string? savedName = null;
        string? savedLink = "unset";
        _members.Setup(r => r.UpdateMemberAsync(It.IsAny<MemberModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MemberModel m, CancellationToken _) =>
                {
                    savedName = m.Name;
                    savedLink = m.IdentityUserId;
                    return m;
                });

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Null(savedLink);
        Assert.Equal("Former member #7", savedName);
        _members.Verify(r => r.DeleteMemberAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Deletes_the_account_and_its_reset_codes_together_then_signs_out()
    {
        GivenLinkedMember();

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(1, _unitOfWork.Commits);
        _resetCodes.Verify(r => r.DeleteAllForUserAsync("user-1", It.IsAny<CancellationToken>()), Times.Once);
        _identity.Verify(s => s.DeleteAccountAsync("user-1", It.IsAny<CancellationToken>()), Times.Once);
        _identity.Verify(s => s.SignOutAsync(), Times.Once);
    }

    // Accounts that predate member linking have no member at all.
    [Fact]
    public async Task Deletes_an_account_that_never_had_a_member()
    {
        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.False(result.IsError);
        _members.Verify(r => r.UpdateMemberAsync(It.IsAny<MemberModel>(), It.IsAny<CancellationToken>()), Times.Never);
        _identity.Verify(s => s.DeleteAccountAsync("user-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    // The member unlink must not survive an account that did: that would
    // orphan the loan history from a sign-in that still exists.
    [Fact]
    public async Task Rolls_everything_back_and_stays_signed_in_when_identity_refuses_the_delete()
    {
        GivenLinkedMember();
        _identity.Setup(s => s.DeleteAccountAsync("user-1", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Error.Unexpected("Account.NotDeleted", "Optimistic concurrency failure."));

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.Equal(ErrorType.Unexpected, result.FirstError.Type);
        Assert.Equal("Account.NotDeleted", result.FirstError.Code);
        Assert.Equal(1, _unitOfWork.Rollbacks);
        Assert.Equal(0, _unitOfWork.Commits);
        _identity.Verify(s => s.SignOutAsync(), Times.Never);
    }
}
