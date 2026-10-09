using ErrorOr;

namespace Application.ServiceInterfaces
{
    public interface IEmailService
    {
        // Returns the failure rather than throwing it. The caller that cares
        // is SendWelcomeEmailJob, which has to translate a failure into the
        // one signal Hangfire understands — see the comment there.
        Task<ErrorOr<Success>> SendWelcomeEmailAsync(
            string email,
            string username);

        Task<ErrorOr<Success>> SendPasswordResetEmailAsync(
            string email,
            string username,
            string code);

        Task<ErrorOr<Success>> SendLoanReceiptEmailAsync(
            string email,
            string username,
            string bookTitle,
            DateTime dueAt);

        Task<ErrorOr<Success>> SendReturnReceiptEmailAsync(
            string email,
            string username,
            string bookTitle,
            DateTime returnedAt,
            bool wasOverdue);
    }
}
