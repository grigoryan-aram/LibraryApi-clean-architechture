namespace Application.IntegrationEvents
{
    public record BookBorrowed(
        int LoanId,
        int BookId,
        string BookTitle,
        int MemberId,
        DateTime BorrowedAt,
        DateTime DueAt);
}
