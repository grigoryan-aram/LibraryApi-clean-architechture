using Application.Features.Registration;
using FluentEmail.Core;
using MediatR;
using Microsoft.Extensions.Logging;
namespace Application.Features.Notifications
{
    public class SendEmailHandler
        : INotificationHandler<UserRegisteredNotification>
    {
        private readonly IFluentEmail _email;
        private readonly ILogger<SendEmailHandler> _logger;

        public SendEmailHandler(
            IFluentEmail email,
            ILogger<SendEmailHandler> logger)
        {
            _email = email;
            _logger = logger;
        }

        public async Task Handle(
            UserRegisteredNotification notification,
            CancellationToken cancellationToken)
        {
            var subject = "Welcome to Library API";
            var body = $"Hello {notification.UserName}, welcome!";

            var response = await _email
                    .To(notification.Email)
                    .Subject(subject)
                    .Body(body)
                    .SendAsync(cancellationToken);

            // FluentEmail reports SMTP failures on the response rather than
            // throwing. These used to go to Console.WriteLine, which means
            // they were invisible anywhere the console is not the log sink.
            if (!response.Successful)
            {
                _logger.LogError(
                    "Failed to send the welcome email to {Email}: {Errors}",
                    notification.Email,
                    string.Join("; ", response.ErrorMessages));

                return;
            }

            _logger.LogInformation(
                "Sent the welcome email to {Email}.",
                notification.Email);
        }
    }
}
