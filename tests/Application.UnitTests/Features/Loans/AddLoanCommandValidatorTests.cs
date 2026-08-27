using Application.Features.Loans.Commands;

namespace Application.UnitTests.Features.Loans;

public class AddLoanCommandValidatorTests
{
    private readonly AddLoanCommandValidator _validator = new();

    // The command carries nothing but the two ids now. The dates it used to
    // accept from the caller — BorrowedAt and ReturnedAt — are the server's
    // business, so the tests that guarded them are gone with them.
    [Fact]
    public void Accepts_a_book_and_a_member()
    {
        var result = _validator.Validate(new AddLoanCommand(BookId: 1, MemberId: 2));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Rejects_a_missing_book(int bookId)
    {
        var result = _validator.Validate(new AddLoanCommand(bookId, 2));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            e => e.PropertyName == nameof(AddLoanCommand.BookId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Rejects_a_missing_member(int memberId)
    {
        var result = _validator.Validate(new AddLoanCommand(1, memberId));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            e => e.PropertyName == nameof(AddLoanCommand.MemberId));
    }
}
