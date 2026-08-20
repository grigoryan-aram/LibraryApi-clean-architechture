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
            await _email
                .To(email)
                .Subject("Welcome")
                .Body($"Welcome, {username}!")
                .SendAsync();
        }
    }
}
