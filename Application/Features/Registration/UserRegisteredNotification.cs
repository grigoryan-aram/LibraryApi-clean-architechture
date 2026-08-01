using MediatR;

namespace Application.Features.Registration
{
    public class UserRegisteredNotification : INotification
    {
        public string Email { get; }
        public string UserName { get; }

        public UserRegisteredNotification(
            string email,
            string userName)
        {
            Email = email;
            UserName = userName;
        }
    }
}
