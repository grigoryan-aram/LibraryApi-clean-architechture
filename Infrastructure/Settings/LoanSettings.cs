namespace Infrastructure.Settings
{
    public class LoanSettings
    {
        // How many days a book may be kept before it counts as overdue.
        // Bound from the "Loans" section; 14 is the default when nothing is
        // configured. Changing it only affects loans handed out afterwards —
        // DueAt is stamped on the row, not recomputed on read.
        public int LoanPeriodDays { get; set; } = 14;
    }
}
