namespace LibraryApi.Application.DTOs
{
    public class LoansDTO
    {

        public int BookId { get; set; }
        public int MemberId { get; set; }
        public DateTime BorrowedAt { get; set; }
        public DateTime? ReturnedAt { get; set; }
    }
}
