namespace LibraryApi.Domain.Entities;


public class LoanModel
{
    public int Id { get; set; }

    public int BookId { get; set; }
    public BookModel? Book { get; set; }

    public int MemberId { get; set; }

    public MemberModel? Member { get; set; }


    public DateTime BorrowedAt { get; set; } = DateTime.MinValue;

    public DateTime? ReturnedAt { get; set; } = null;

}



