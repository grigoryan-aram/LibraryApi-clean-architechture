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

namespace Application.UnitTests.IntegrationEvents;

/// <summary>
/// The rule that matters for every publish site: the event goes out after the
/// write, and a broker that is down never fails the request.
/// </summary>
public class EventPublishingTests
{
    private readonly Mock<IEventPublisher> _events = new();

    private static readonly ErrorOr<Success> BrokerDown = Error.Failure(
        "Events.PublishFailed", "Could not publish: connection refused.");

    private void GivenTheBrokerIsDown() =>
        _events.Setup(e => e.PublishAsync(
                   It.IsAny<It.IsAnyType>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(BrokerDown);

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
    public async Task Lending_still_succeeds_when_the_broker_is_down()
    {
        GivenALendableBookAndMember();
        GivenTheBrokerIsDown();

        var result = await CreateLendSut().Handle(
            new AddLoanCommand(BookId: 1, MemberId: 2), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(99, result.Value.Id);
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
        new(_loans.Object, _events.Object, NullLogger<ReturnLoanCommandHandler>.Instance);

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

    [Fact]
    public async Task Registering_still_succeeds_and_still_queues_the_email_when_the_broker_is_down()
    {
        GivenRegistrationSucceeds();
        GivenTheBrokerIsDown();

        var result = await CreateRegisterSut().Handle(
            new RegisterCommand("ada", "Pa55word!", "ada@example.com"),
            CancellationToken.None);

        Assert.False(result.IsError);
        _jobs.Verify(c => c.Create(
            It.Is<Job>(job => job.Type == typeof(SendWelcomeEmailJob)),
            It.Is<IState>(state => state is EnqueuedState)), Times.Once);
    }
}
