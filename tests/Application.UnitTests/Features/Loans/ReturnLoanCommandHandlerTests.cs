using Application.Features.Loans.Commands;
using Application.RepositoryInterfaces;
using ErrorOr;
using LibraryApi.Domain.Entities;
using Moq;

namespace Application.UnitTests.Features.Loans;

public class ReturnLoanCommandHandlerTests
{
    private readonly Mock<ILoansRepository> _loans = new();

    private ReturnLoanCommandHandler CreateSut() => new(_loans.Object);

    private void GivenLoan(LoanModel? loan) =>
        _loans.Setup(repo => repo.GetLoanByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(loan);

    private void GivenTheUpdateSticks() =>
        _loans.Setup(repo => repo.UpdateLoanAsync(It.IsAny<LoanModel>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((LoanModel loan, CancellationToken _) => loan);

    private static LoanModel OpenLoan(DateTime? dueAt = null) => new()
    {
        Id = 5,
        BookId = 1,
        MemberId = 2,
        BorrowedAt = DateTime.UtcNow.AddDays(-3),
        DueAt = dueAt ?? DateTime.UtcNow.AddDays(11),
        ReturnedAt = null
    };

    [Fact]
    public async Task Stamps_the_return_date_and_saves_it()
    {
        GivenLoan(OpenLoan());
        GivenTheUpdateSticks();

        var before = DateTime.UtcNow;
        var result = await CreateSut().Handle(new ReturnLoanCommand(5), CancellationToken.None);
        var after = DateTime.UtcNow;

        Assert.False(result.IsError);
        Assert.NotNull(result.Value.ReturnedAt);
        Assert.InRange(result.Value.ReturnedAt!.Value, before, after);
        Assert.True(result.Value.IsReturned);
        _loans.Verify(repo => repo.UpdateLoanAsync(
            It.Is<LoanModel>(loan => loan.ReturnedAt != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // A returned loan is closed. Letting a second return through would move
    // ReturnedAt forward and rewrite when the book actually came back.
    [Fact]
    public async Task Refuses_a_second_return_without_writing_anything()
    {
        GivenLoan(new LoanModel
        {
            Id = 5,
            BorrowedAt = DateTime.UtcNow.AddDays(-5),
            DueAt = DateTime.UtcNow.AddDays(9),
            ReturnedAt = DateTime.UtcNow.AddDays(-1)
        });

        var result = await CreateSut().Handle(new ReturnLoanCommand(5), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Conflict, result.FirstError.Type);
        Assert.Equal("Loans.AlreadyReturned", result.FirstError.Code);
        _loans.Verify(repo => repo.UpdateLoanAsync(
            It.IsAny<LoanModel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reports_a_loan_that_is_not_there_as_not_found()
    {
        GivenLoan(null);

        var result = await CreateSut().Handle(new ReturnLoanCommand(404), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
        Assert.Equal("Loans.NotFound", result.FirstError.Code);
        _loans.Verify(repo => repo.UpdateLoanAsync(
            It.IsAny<LoanModel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Returning it late is still returning it, so the overdue flag has to
    // clear — that is what makes GET /api/Loans/overdue shrink.
    [Fact]
    public async Task Closes_an_overdue_loan_too()
    {
        GivenLoan(OpenLoan(dueAt: DateTime.UtcNow.AddDays(-2)));
        GivenTheUpdateSticks();

        var result = await CreateSut().Handle(new ReturnLoanCommand(5), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.True(result.Value.IsReturned);
        Assert.False(result.Value.IsOverdue);
        Assert.Equal(0, result.Value.DaysOverdue);
    }
}
