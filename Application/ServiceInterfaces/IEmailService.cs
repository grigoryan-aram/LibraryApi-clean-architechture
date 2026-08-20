namespace Application.ServiceInterfaces
{
    public interface IEmailService
    {
        Task SendWelcomeEmailAsync(
            string email,
            string username);
    }
}
