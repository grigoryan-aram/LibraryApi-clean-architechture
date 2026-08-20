namespace Infrastructure.Services
{
    using Application.Jobs;
    using Application.ServiceInterfaces;
    using Hangfire;

    public class BackgroundJobService : IBackgroundJobService
    {
        private readonly IBackgroundJobClient _client;

        public BackgroundJobService(
            IBackgroundJobClient client)
        {
            _client = client;
        }

        public void EnqueueWelcomeEmail(
            string email,
            string username)
        {
            _client.Enqueue<SendWelcomeEmailJob>(
                job => job.ExecuteAsync(email, username));
        }
    }
}
