using Application.Features.Loans.Queries;
using Application.RepositoryInterfaces;
using LibraryApi.Domain.Entities;
using Moq;

namespace Application.UnitTests.Features.Loans;

public class GetOverdueLoansQueryHandlerTests
{
    private readonly Mock<ILoansRepository> _loans = new();

    private GetOverdueLoansQueryHandler CreateSut() => new(_loans.Object);

    [Fact]
    public async Task Asks_the_repository_for_loans_overdue_as_of_now()
    {
        var before = DateTime.UtcNow;

        _loans.Setup(repo => repo.GetOverdueLoansAsync(
                  It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync([]);

        await CreateSut().Handle(new GetOverdueLoansQuery(), CancellationToken.None);

        var after = DateTime.UtcNow;

        _loans.Verify(repo => repo.GetOverdueLoansAsync(
            It.Is<DateTime>(asOf => asOf >= before && asOf <= after),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // Nothing overdue is a good answer, not a missing one. Returning NotFound
    // here — as GetAllLoansQuery does for an empty table — would make a
    // healthy library look like a broken endpoint.
    [Fact]
    public async Task Returns_an_empty_list_rather_than_not_found()
    {
        _loans.Setup(repo => repo.GetOverdueLoansAsync(
                  It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync([]);

        var result = await CreateSut().Handle(new GetOverdueLoansQuery(), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task Maps_every_overdue_loan_it_is_given()
    {
        var dueAt = DateTime.UtcNow.AddDays(-3);

        _loans.Setup(repo => repo.GetOverdueLoansAsync(
                  It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(
              [
                  new LoanModel
                  {
                      Id = 1, BookId = 7, MemberId = 2,
                      BorrowedAt = DateTime.UtcNow.AddDays(-17),
                      DueAt = dueAt
                  }
              ]);

        var result = await CreateSut().Handle(new GetOverdueLoansQuery(), CancellationToken.None);

        var loan = Assert.Single(result.Value);
        Assert.Equal(7, loan.BookId);
        Assert.Equal(dueAt, loan.DueAt);
        Assert.True(loan.IsOverdue);

        // Three days and a few microseconds late is three days overdue.
        // DaysOverdue used to round up and call this four.
        Assert.Equal(3, loan.DaysOverdue);
    }
}
