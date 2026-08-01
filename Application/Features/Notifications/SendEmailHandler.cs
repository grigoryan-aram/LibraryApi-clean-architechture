using Application.Features.Registration;
using FluentEmail.Core;
using MediatR;
namespace Application.Features.Notifications
{
    public class SendEmailHandler
        : INotificationHandler<UserRegisteredNotification>
    {
        private readonly IFluentEmail _email;

        public SendEmailHandler(IFluentEmail email)
        {
            _email = email;
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

            if (!response.Successful)
            {
                foreach (var error in response.ErrorMessages)
                {
                    Console.WriteLine(error);
                }

            }

        }
    }
}




