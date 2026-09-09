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
        var result = await _emailService.SendWelcomeEmailAsync(
            email,
            username);

        if (result.IsError)
        {

            // The only place that throws an exception.
            throw new InvalidOperationException(
                string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }
}
