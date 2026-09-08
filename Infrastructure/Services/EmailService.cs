using Application.ServiceInterfaces;
using ErrorOr;
using FluentEmail.Core;
using FluentEmail.Core.Models;
using Microsoft.Extensions.Logging;


namespace Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly IFluentEmail _email;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IFluentEmail email, ILogger<EmailService> logger)
        {
            _email = email;

            _logger = logger;
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
            catch (Exception ex)
            {
               
                _logger.LogError(
                    ex,
                    "Failed to send welcome email to {Email}.",
                    email);

                return Error.Failure(
                    "Email.SendFailed",
                    $"Failed to send welcome email to {email}: {ex.Message}");
            }

            // FluentEmail reports SMTP failures on the response instead of
            // throwing. Without this check a failed send is indistinguishable
            // from a successful one, which is how a lost email used to be
            // recorded as a succeeded Hangfire job.
            if (!response.Successful)
            {
                _logger.LogError(
                    "Failed to send welcome email to {Email}: {Errors}",
                    email,
                    string.Join("; ", response.ErrorMessages));

                return Error.Failure(
                    "Email.SendFailed",
                    $"Failed to send welcome email to {email}: " +
                    string.Join("; ", response.ErrorMessages));
            }

            return Result.Success;
        }
    }
}
