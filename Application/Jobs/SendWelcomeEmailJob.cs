using Application.ServiceInterfaces;

namespace Application.Jobs;

public class SendWelcomeEmailJob
{
    private readonly IEmailService _emailService;

    public SendWelcomeEmailJob(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task ExecuteAsync(
        string email,
        string username)
    {
        await _emailService.SendWelcomeEmailAsync(
            email,
            username);
    }
}