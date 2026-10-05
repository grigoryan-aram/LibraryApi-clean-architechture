namespace Application.IntegrationEvents
{
    public record BookReturned(
        int LoanId,
        int BookId,
        int MemberId,
        DateTime BorrowedAt,
        DateTime DueAt,
        DateTime ReturnedAt)
    {
        public bool WasOverdue => ReturnedAt > DueAt;
    }
}
