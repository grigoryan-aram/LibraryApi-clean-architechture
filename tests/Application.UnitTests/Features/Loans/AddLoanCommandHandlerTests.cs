using Application.Features.Loans.Commands;
using Application.RepositoryInterfaces;
using Application.ServiceInterfaces;
using ErrorOr;
using LibraryApi.Application.RepositoryInterfaces;
using LibraryApi.Domain.Entities;
using Moq;

namespace Application.UnitTests.Features.Loans;

public class AddLoanCommandHandlerTests
{
    private const int Period = 14;

    private readonly Mock<ILoansRepository> _loans = new();
    private readonly Mock<IBooksRepository> _books = new();
    private readonly Mock<IMembersRepository> _members = new();

    // A stub rather than a mock: the policy is pure arithmetic, and a fixed
    // period makes the due-date assertion exact.
    private sealed class FixedLoanPolicy : ILoanPolicy
    {
        public int LoanPeriodDays => Period;

        public DateTime DueDateFor(DateTime borrowedAt) =>
            borrowedAt.AddDays(Period);
    }

    private AddLoanCommandHandler CreateSut() =>
        new(_loans.Object, _books.Object, _members.Object, new FixedLoanPolicy());

    private void GivenBookExists(int id = 1) =>
        _books.Setup(repo => repo.GetByIdAsync(id, It.IsAny<CancellationToken>()))
              .ReturnsAsync(new BookModel { Id = id, Title = "Dune" });

    private void GivenMemberExists(int id = 2) =>
        _members.Setup(repo => repo.GetMemberByIdAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MemberModel { Id = id, Name = "Ada" });

    // Echo the loan back with an id, the way the real repository does.
    private void GivenTheLoanIsSaved() =>
        _loans.Setup(repo => repo.AddLoanAsync(It.IsAny<LoanModel>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((LoanModel loan, CancellationToken _) =>
              {
                  loan.Id = 99;
                  return loan;
              });

    private static AddLoanCommand Command => new(BookId: 1, MemberId: 2);

    // An unknown id used to reach SQL Server and come back as a foreign-key
    // violation, which the exception middleware turned into a 500. A bad id in
    // the request is not a server error.
    [Fact]
    public async Task Refuses_a_book_that_does_not_exist_without_writing_anything()
    {
        GivenMemberExists();
        _books.Setup(repo => repo.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((BookModel?)null);

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
        Assert.Equal("Loans.BookNotFound", result.FirstError.Code);
        _loans.Verify(repo => repo.AddLoanAsync(
            It.IsAny<LoanModel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Refuses_a_member_that_does_not_exist_without_writing_anything()
    {
        GivenBookExists();
        _members.Setup(repo => repo.GetMemberByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MemberModel?)null);

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
        Assert.Equal("Loans.MemberNotFound", result.FirstError.Code);
        _loans.Verify(repo => repo.AddLoanAsync(
            It.IsAny<LoanModel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // BorrowedAt used to arrive from the caller and DueAt did not exist at
    // all. Both now come from the server, and the loan opens unreturned.
    [Fact]
    public async Task Stamps_the_borrow_date_and_the_due_date_itself()
    {
        GivenBookExists();
        GivenMemberExists();
        GivenTheLoanIsSaved();

        var before = DateTime.UtcNow;
        var result = await CreateSut().Handle(Command, CancellationToken.None);
        var after = DateTime.UtcNow;

        Assert.False(result.IsError);
        Assert.InRange(result.Value.BorrowedAt, before, after);
        Assert.Equal(result.Value.BorrowedAt.AddDays(Period), result.Value.DueAt);
        Assert.Null(result.Value.ReturnedAt);
        Assert.False(result.Value.IsOverdue);
        Assert.False(result.Value.IsReturned);
    }

    [Fact]
    public async Task Saves_the_loan_against_the_requested_book_and_member()
    {
        GivenBookExists();
        GivenMemberExists();
        GivenTheLoanIsSaved();

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.Equal(99, result.Value.Id);
        _loans.Verify(repo => repo.AddLoanAsync(
            It.Is<LoanModel>(loan => loan.BookId == 1 && loan.MemberId == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
