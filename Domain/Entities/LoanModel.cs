namespace LibraryApi.Domain.Entities;


public class LoanModel
{
    public int Id { get; set; }

    public int BookId { get; set; }
    public BookModel? Book { get; set; }

    public int MemberId { get; set; }

    public MemberModel? Member { get; set; }


    public DateTime BorrowedAt { get; set; } = DateTime.MinValue;

    // When the book is due back. Stamped by the handler from ILoanPolicy at
    // the moment of lending, not supplied by the caller — a client-chosen due
    // date is not a due date. It is stored rather than computed on read so
    // that changing the configured loan period cannot retroactively make
    // yesterday's loans overdue.
    public DateTime DueAt { get; set; } = DateTime.MinValue;

    public DateTime? ReturnedAt { get; set; } = null;

}
