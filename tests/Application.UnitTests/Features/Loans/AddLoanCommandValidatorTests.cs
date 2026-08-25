using Application.Features.Loans.Commands;

namespace Application.UnitTests.Features.Loans;

public class AddLoanCommandValidatorTests
{
    private readonly AddLoanCommandValidator _validator = new();

    private static AddLoanCommand Loan(DateTime? returnedAt) =>
        new(BookId: 1, MemberId: 2, BorrowedAt: DateTime.UtcNow.AddDays(-1), ReturnedAt: returnedAt);

    // The whole point of a loan endpoint: hand a book out now, with no return
    // date. The validator used to require ReturnedAt, which made an open loan
    // impossible to create and the endpoint useless.
    [Fact]
    public void Accepts_an_open_loan_with_no_return_date()
    {
        var result = _validator.Validate(Loan(returnedAt: null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Accepts_a_closed_loan_returned_after_it_was_borrowed()
    {
        var result = _validator.Validate(Loan(returnedAt: DateTime.UtcNow));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Rejects_a_return_date_before_the_borrow_date()
    {
        var result = _validator.Validate(Loan(returnedAt: DateTime.UtcNow.AddDays(-5)));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            e => e.PropertyName == nameof(AddLoanCommand.ReturnedAt));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Rejects_a_missing_book(int bookId)
    {
        var result = _validator.Validate(
            new AddLoanCommand(bookId, 2, DateTime.UtcNow, null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            e => e.PropertyName == nameof(AddLoanCommand.BookId));
    }

    [Fact]
    public void Rejects_a_borrow_date_in_the_future()
    {
        var result = _validator.Validate(
            new AddLoanCommand(1, 2, DateTime.UtcNow.AddDays(1), null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            e => e.PropertyName == nameof(AddLoanCommand.BorrowedAt));
    }
}
