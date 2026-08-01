using FluentEmail.Core;


namespace Infrastructure.Services
{
    public class EmailService
    {
        private readonly IFluentEmail _email;

        public EmailService(IFluentEmail email)
        {
            _email = email;
        }

        public async Task SendEmailAsync(
            string to,
            string subject,
            string body,
            CancellationToken cancellationToken)
        {
            await _email
                .To(to)
                .Subject(subject)
                .Body(body)
                .SendAsync(cancellationToken);
        }
    }
}
