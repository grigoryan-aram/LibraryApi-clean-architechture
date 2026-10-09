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

        public Task<ErrorOr<Success>> SendWelcomeEmailAsync(
            string email,
            string username) =>
            SendAsync(
                email,
                "welcome email",
                "Welcome",
                $"Welcome, {username}!");

        // The code itself is never logged: the log is the one place a
        // short-lived secret would outlive its window. SendAsync logs only the
        // address.
        public Task<ErrorOr<Success>> SendPasswordResetEmailAsync(
            string email,
            string username,
            string code) =>
            SendAsync(
                email,
                "a password reset email",
                "Your password reset code",
                $"Hello {username},\r\n\r\n" +
                $"Your password reset code is: {code}\r\n\r\n" +
                $"It expires in {PasswordResetRules.ExpiryMinutes} minutes and can be " +
                $"used once. If you did not ask to reset your password, " +
                $"ignore this email — nothing has changed.");

        public Task<ErrorOr<Success>> SendLoanReceiptEmailAsync(
            string email,
            string username,
            string bookTitle,
            DateTime dueAt) =>
            SendAsync(
                email,
                "a loan receipt",
                $"You borrowed \"{bookTitle}\"",
                $"Hello {username},\r\n\r\n" +
                $"You borrowed \"{bookTitle}\". Please return it by " +
                $"{dueAt:dddd d MMMM yyyy} (UTC).");

        public Task<ErrorOr<Success>> SendReturnReceiptEmailAsync(
            string email,
            string username,
            string bookTitle,
            DateTime returnedAt,
            bool wasOverdue) =>
            SendAsync(
                email,
                "a return receipt",
                $"You returned \"{bookTitle}\"",
                $"Hello {username},\r\n\r\n" +
                $"We received \"{bookTitle}\" back on {returnedAt:dddd d MMMM yyyy} (UTC)." +
                (wasOverdue
                    ? " It came back after its due date — please keep an eye on " +
                      "due dates for future loans."
                    : " Thank you for returning it on time."));

        private async Task<ErrorOr<Success>> SendAsync(
            string email,
            string what,
            string subject,
            string body)
        {
            SendResponse response;

            try
            {
                response = await _email
                    .To(email)
                    .Subject(subject)
                    .Body(body)
                    .SendAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send {What} to {Email}.",
                    what,
                    email);

                return Error.Failure(
                    "Email.SendFailed",
                    $"Failed to send {what} to {email}: {ex.Message}");
            }

            // FluentEmail reports SMTP failures on the response instead of
            // throwing. Without this check a failed send is indistinguishable
            // from a successful one, which is how a lost email used to be
            // recorded as a succeeded Hangfire job.
            if (!response.Successful)
            {
                _logger.LogError(
                    "Failed to send {What} to {Email}: {Errors}",
                    what,
                    email,
                    string.Join("; ", response.ErrorMessages));

                return Error.Failure(
                    "Email.SendFailed",
                    $"Failed to send {what} to {email}: " +
                    string.Join("; ", response.ErrorMessages));
            }

            return Result.Success;
        }
    }
}
