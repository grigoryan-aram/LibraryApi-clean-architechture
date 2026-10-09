using Application.DTOs;
using Application.Features.Loans.Commands;
using Application.Features.Registration;
using Application.IntegrationEvents;
using Application.Jobs;
using Application.ServiceInterfaces;
using ErrorOr;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using LibraryApi.Domain.Entities;
using LibraryApi.Domain.RepositoryInterfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

using Application.UnitTests.TestDoubles;

namespace Application.UnitTests.IntegrationEvents;

/// <summary>
/// The rule that matters for every publish site: the write and the event that
/// announces it commit together or not at all. The broker is not in this
/// picture any more — the publisher writes to the outbox table, so "broker
/// down" cannot fail a request; only failing to record the event can.
/// </summary>
public class EventPublishingTests
{
    private readonly Mock<IEventPublisher> _events = new();
    private readonly RecordingUnitOfWork _unitOfWork = new();

    private static readonly ErrorOr<Success> NotRecorded = Error.Unexpected(
        "Events.NotRecorded", "Could not record the event.");

    private void GivenTheEventCannotBeRecorded() =>
        _events.Setup(e => e.PublishAsync(
                   It.IsAny<It.IsAnyType>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(NotRecorded);

    // ---------- BookBorrowed ----------

    private sealed class FixedLoanPolicy : ILoanPolicy
    {
        public int LoanPeriodDays => 14;

        public DateTime DueDateFor(DateTime borrowedAt) => borrowedAt.AddDays(14);
    }

    private readonly Mock<ILoansRepository> _loans = new();
    private readonly Mock<IBooksRepository> _books = new();
    private readonly Mock<IMembersRepository> _members = new();

    private AddLoanCommandHandler CreateLendSut() =>
        new(_loans.Object,
            _books.Object,
            _members.Object,
            new FixedLoanPolicy(),
            _events.Object,
            _unitOfWork,
            NullLogger<AddLoanCommandHandler>.Instance);

    private void GivenALendableBookAndMember()
    {
        _books.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
              .ReturnsAsync(new BookModel { Id = 1, Title = "Dune", TotalCopies = 2 });
        _members.Setup(r => r.GetMemberByIdAsync(2, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MemberModel { Id = 2, Name = "Ada" });
        _loans.Setup(r => r.AddLoanAsync(It.IsAny<LoanModel>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((LoanModel loan, CancellationToken _) =>
              {
                  loan.Id = 99;
                  return loan;
              });
    }

    [Fact]
    public async Task Lending_publishes_BookBorrowed_carrying_the_saved_loan()
    {
        GivenALendableBookAndMember();

        await CreateLendSut().Handle(
            new AddLoanCommand(BookId: 1, MemberId: 2), CancellationToken.None);

        _events.Verify(e => e.PublishAsync(
            It.Is<BookBorrowed>(evt =>
                evt.LoanId == 99
                && evt.BookId == 1
                && evt.BookTitle == "Dune"
                && evt.MemberId == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Lending_commits_the_loan_and_its_event_together()
    {
        GivenALendableBookAndMember();

        var result = await CreateLendSut().Handle(
            new AddLoanCommand(BookId: 1, MemberId: 2), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(99, result.Value.Id);
        Assert.Equal(1, _unitOfWork.Commits);
        Assert.Equal(0, _unitOfWork.Rollbacks);
    }

    // A loan nobody is told about is the inconsistency the outbox exists to
    // prevent, so the loan goes too.
    [Fact]
    public async Task Lending_is_rolled_back_when_its_event_cannot_be_recorded()
    {
        GivenALendableBookAndMember();
        GivenTheEventCannotBeRecorded();

        var result = await CreateLendSut().Handle(
            new AddLoanCommand(BookId: 1, MemberId: 2), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Unexpected, result.FirstError.Type);
        Assert.Equal("Events.NotRecorded", result.FirstError.Code);
        Assert.Equal(0, _unitOfWork.Commits);
        Assert.Equal(1, _unitOfWork.Rollbacks);
    }

    // The loan row must exist before anyone is told about it, or a consumer
    // can read back a loan that is not there yet.
    [Fact]
    public async Task Lending_publishes_nothing_when_the_loan_was_refused()
    {
        _books.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((BookModel?)null);
        _members.Setup(r => r.GetMemberByIdAsync(2, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MemberModel { Id = 2, Name = "Ada" });

        await CreateLendSut().Handle(
            new AddLoanCommand(BookId: 1, MemberId: 2), CancellationToken.None);

        _events.Verify(e => e.PublishAsync(
            It.IsAny<It.IsAnyType>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- BookReturned ----------

    private ReturnLoanCommandHandler CreateReturnSut() =>
        new(_loans.Object, _events.Object, _unitOfWork, NullLogger<ReturnLoanCommandHandler>.Instance);

    private void GivenAnOpenLoan(DateTime dueAt)
    {
        _loans.Setup(r => r.GetLoanByIdAsync(5, It.IsAny<CancellationToken>()))
              .ReturnsAsync(new LoanModel
              {
                  Id = 5,
                  BookId = 1,
                  MemberId = 2,
                  BorrowedAt = DateTime.UtcNow.AddDays(-20),
                  DueAt = dueAt,
                  ReturnedAt = null
              });
        _loans.Setup(r => r.UpdateLoanAsync(It.IsAny<LoanModel>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((LoanModel loan, CancellationToken _) => loan);
    }

    [Fact]
    public async Task Returning_publishes_BookReturned()
    {
        GivenAnOpenLoan(DateTime.UtcNow.AddDays(-6));

        await CreateReturnSut().Handle(new ReturnLoanCommand(5), CancellationToken.None);

        _events.Verify(e => e.PublishAsync(
            It.Is<BookReturned>(evt => evt.LoanId == 5 && evt.BookId == 1 && evt.MemberId == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Returning_is_rolled_back_when_its_event_cannot_be_recorded()
    {
        GivenAnOpenLoan(DateTime.UtcNow.AddDays(3));
        GivenTheEventCannotBeRecorded();

        var result = await CreateReturnSut().Handle(new ReturnLoanCommand(5), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Events.NotRecorded", result.FirstError.Code);
        Assert.Equal(1, _unitOfWork.Rollbacks);
        Assert.Equal(0, _unitOfWork.Commits);
    }

    // WasOverdue is computed on the event, so a consumer cannot disagree with
    // the library about whether the book came back late.
    [Fact]
    public async Task BookReturned_reports_whether_it_came_back_late()
    {
        GivenAnOpenLoan(DateTime.UtcNow.AddDays(-6));
        await CreateReturnSut().Handle(new ReturnLoanCommand(5), CancellationToken.None);
        _events.Verify(e => e.PublishAsync(
            It.Is<BookReturned>(evt => evt.WasOverdue), It.IsAny<CancellationToken>()), Times.Once);

        _events.Reset();

        GivenAnOpenLoan(DateTime.UtcNow.AddDays(10));
        await CreateReturnSut().Handle(new ReturnLoanCommand(5), CancellationToken.None);
        _events.Verify(e => e.PublishAsync(
            It.Is<BookReturned>(evt => !evt.WasOverdue), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Returning_publishes_nothing_for_a_loan_already_returned()
    {
        _loans.Setup(r => r.GetLoanByIdAsync(5, It.IsAny<CancellationToken>()))
              .ReturnsAsync(new LoanModel
              {
                  Id = 5,
                  BookId = 1,
                  MemberId = 2,
                  ReturnedAt = DateTime.UtcNow.AddDays(-1)
              });

        await CreateReturnSut().Handle(new ReturnLoanCommand(5), CancellationToken.None);

        _events.Verify(e => e.PublishAsync(
            It.IsAny<It.IsAnyType>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- MemberRegistered ----------

    private readonly Mock<IIdentityService> _identity = new();
    private readonly Mock<IBackgroundJobClient> _jobs = new();

    private global::RegisterCommandHandler CreateRegisterSut() =>
        new(_identity.Object,
            _members.Object,
            _jobs.Object,
            _events.Object,
            _unitOfWork,
            NullLogger<global::RegisterCommandHandler>.Instance);

    private void GivenRegistrationSucceeds()
    {
        _identity.Setup(s => s.RegisterAsync(
                     It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                     It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new RegisteredUserDTO("user-1", "ada", "ada@example.com"));
        _members.Setup(r => r.AddMemberAsync(
                    It.IsAny<MemberModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MemberModel m, CancellationToken _) =>
                {
                    m.Id = 7;
                    return m;
                });
    }

    [Fact]
    public async Task Registering_publishes_MemberRegistered_with_the_new_member_id()
    {
        GivenRegistrationSucceeds();

        await CreateRegisterSut().Handle(
            new RegisterCommand("ada", "Pa55word!", "ada@example.com"),
            CancellationToken.None);

        _events.Verify(e => e.PublishAsync(
            It.Is<MemberRegistered>(evt =>
                evt.Username == "ada"
                && evt.Email == "ada@example.com"
                && evt.MemberId == 7),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // The event carries a member id, and a failed link leaves nothing honest
    // to put in it.
    [Fact]
    public async Task Registering_publishes_nothing_when_the_member_could_not_be_created()
    {
        GivenRegistrationSucceeds();
        _members.Setup(r => r.AddMemberAsync(
                    It.IsAny<MemberModel>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("table is missing"));

        var result = await CreateRegisterSut().Handle(
            new RegisterCommand("ada", "Pa55word!", "ada@example.com"),
            CancellationToken.None);

        Assert.False(result.IsError);
        _events.Verify(e => e.PublishAsync(
            It.IsAny<MemberRegistered>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // The account already exists, so registration still succeeds — but the
    // member and its event go together, and the welcome email is independent
    // of both.
    [Fact]
    public async Task Registering_rolls_back_the_member_but_still_succeeds_and_queues_the_email_when_the_event_cannot_be_recorded()
    {
        GivenRegistrationSucceeds();
        GivenTheEventCannotBeRecorded();

        var result = await CreateRegisterSut().Handle(
            new RegisterCommand("ada", "Pa55word!", "ada@example.com"),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(1, _unitOfWork.Rollbacks);
        Assert.Equal(0, _unitOfWork.Commits);
        _jobs.Verify(c => c.Create(
            It.Is<Job>(job => job.Type == typeof(SendWelcomeEmailJob)),
            It.Is<IState>(state => state is EnqueuedState)), Times.Once);
    }
}
