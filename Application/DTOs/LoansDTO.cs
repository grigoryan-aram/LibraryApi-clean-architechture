namespace Application.DTOs
{
    public record LoansDTO(
        int Id,
        int BookId,
        int MemberId,
        DateTime BorrowedAt,
        DateTime DueAt,
        DateTime? ReturnedAt)
    {
        // Computed, not stored and not mapped: Mapster fills the constructor
        // parameters by name and leaves get-only members alone. Working them
        // out here means every caller — the API, the Blazor page, the overdue
        // query — agrees on what "overdue" means.
        public bool IsReturned => ReturnedAt is not null;

        public bool IsOverdue => !IsReturned && DueAt < DateTime.UtcNow;

        // Whole days only, and rounded DOWN: a book three days and one
        // millisecond late is three days overdue, not four, which is what
        // rounding up claimed. The cost is that anything late by less than a
        // day reports 0 — so read it with IsOverdue, never on its own.
        public int DaysOverdue => IsOverdue
            ? (int)(DateTime.UtcNow - DueAt).TotalDays
            : 0;
    }

}
