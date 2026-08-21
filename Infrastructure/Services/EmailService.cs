using Application.ServiceInterfaces;
using FluentEmail.Core;


namespace Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly IFluentEmail _email;

        public EmailService(IFluentEmail email)
        {
            _email = email;
        }

        public async Task SendWelcomeEmailAsync(
            string email,
            string username)
        {
            var response = await _email
                .To(email)
                .Subject("Welcome")
                .Body($"Welcome, {username}!")
                .SendAsync();

            // FluentEmail swallows SMTP exceptions and reports them on the
            // response instead of throwing. Without this check a failed send
            // looks like a successful Hangfire job and is never retried.
            if (!response.Successful)
            {
                throw new InvalidOperationException(
                    $"Failed to send welcome email to {email}: " +
                    string.Join("; ", response.ErrorMessages));
            }
        }
    }
}
