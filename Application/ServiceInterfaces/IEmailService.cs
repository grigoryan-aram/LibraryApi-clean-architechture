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
    }
}
