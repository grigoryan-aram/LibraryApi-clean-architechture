using Application.ServiceInterfaces;
using ErrorOr;
using FluentEmail.Core;
using FluentEmail.Core.Models;


namespace Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly IFluentEmail _email;

        public EmailService(IFluentEmail email)
        {
            _email = email;
        }

        public async Task<ErrorOr<Success>> SendWelcomeEmailAsync(
            string email,
            string username)
        {
            SendResponse response;

            try
            {
                response = await _email
                    .To(email)
                    .Subject("Welcome")
                    .Body($"Welcome, {username}!")
                    .SendAsync();
            }
            catch (Exception exception)
            {
                // FluentEmail's MailKit sender catches SMTP errors and reports
                // them on the response, but it is a third-party library and
                // this method promises not to throw. Map anything that escapes
                // rather than letting it past.
                return Error.Failure(
                    "Email.SendFailed",
                    $"Failed to send welcome email to {email}: {exception.Message}");
            }

            // FluentEmail reports SMTP failures on the response instead of
            // throwing. Without this check a failed send is indistinguishable
            // from a successful one, which is how a lost email used to be
            // recorded as a succeeded Hangfire job.
            if (!response.Successful)
            {
                return Error.Failure(
                    "Email.SendFailed",
                    $"Failed to send welcome email to {email}: " +
                    string.Join("; ", response.ErrorMessages));
            }

            return Result.Success;
        }
    }
}
