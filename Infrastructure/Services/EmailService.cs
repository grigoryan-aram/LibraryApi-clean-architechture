using Application.ServiceInterfaces;
using ErrorOr;
using FluentEmail.Core;
using FluentEmail.Core.Models;
using LibraryApi.Domain.Constants;
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

        public async Task<ErrorOr<Success>> SendPasswordResetEmailAsync(
            string email,
            string username,
            string code)
        {
            SendResponse response;

            try
            {
                response = await _email
                    .To(email)
                    .Subject("Your password reset code")
                    .Body(
                        $"Hello {username},\r\n\r\n" +
                        $"Your password reset code is: {code}\r\n\r\n" +
                        $"It expires in {PasswordResetRules.ExpiryMinutes} minutes and can be " +
                        $"used once. If you did not ask to reset your password, " +
                        $"ignore this email — nothing has changed.")
                    .SendAsync();
            }
            catch (Exception ex)
            {
                // The code itself is never logged: the log is the one place a
                // short-lived secret would outlive its window.
                _logger.LogError(
                    ex,
                    "Failed to send a password reset email to {Email}.",
                    email);

                return Error.Failure(
                    "Email.SendFailed",
                    $"Failed to send a password reset email to {email}: {ex.Message}");
            }

            if (!response.Successful)
            {
                _logger.LogError(
                    "Failed to send a password reset email to {Email}: {Errors}",
                    email,
                    string.Join("; ", response.ErrorMessages));

                return Error.Failure(
                    "Email.SendFailed",
                    $"Failed to send a password reset email to {email}: " +
                    string.Join("; ", response.ErrorMessages));
            }

            return Result.Success;
        }
    }
}
