namespace Application.DTOs
{
    public record LoansDTO(
        int BookId,
        int MemberId,
        DateTime BorrowedAt,
        DateTime? ReturnedAt);

}
