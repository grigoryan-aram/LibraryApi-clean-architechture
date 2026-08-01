namespace LibraryApi.Application.DTOs
{
    public class LoansDTO
    {

        public int BookId { get; init; }
        public int MemberId { get; init; }
        public DateTime BorrowedAt { get; init; }
        public DateTime? ReturnedAt { get; init; }
    }
}
