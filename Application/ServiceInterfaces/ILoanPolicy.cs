namespace Application.ServiceInterfaces
{
    // How long a book may be kept. An Application-owned abstraction rather
    // than IOptions<T> read directly in the handler: the loan period comes
    // from configuration, which is an Infrastructure concern, and this keeps
    // the handler testable without a config stack.
    public interface ILoanPolicy
    {
        int LoanPeriodDays { get; }

        DateTime DueDateFor(DateTime borrowedAt);
    }
}
