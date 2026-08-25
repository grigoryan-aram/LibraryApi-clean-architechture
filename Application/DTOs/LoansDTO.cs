namespace Application.DTOs
{
    public record LoansDTO(
        int Id,
        int BookId,
        int MemberId,
        DateTime BorrowedAt,
        DateTime? ReturnedAt);

}
