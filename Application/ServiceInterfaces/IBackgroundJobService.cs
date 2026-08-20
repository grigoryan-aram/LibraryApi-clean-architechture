namespace Application.ServiceInterfaces
{
    public interface IBackgroundJobService
    {
        void EnqueueWelcomeEmail(
            string email,
            string username);
    }
}
