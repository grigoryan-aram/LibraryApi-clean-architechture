using Application.ServiceInterfaces;

namespace Application.Jobs;

public class SendPasswordResetEmailJob
{
    private readonly IEmailService _emailService;

    public SendPasswordResetEmailJob(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task ExecuteAsync(
        string email,
        string username,
        string code)
    {
        var result = await _emailService.SendPasswordResetEmailAsync(
            email,
            username,
            code);

        if (result.IsError)
        {
            // Throwing is the only vocabulary Hangfire has: a job that returns
            // is recorded as Succeeded, and a returned ErrorOr is still a
            // return. Same reason SendWelcomeEmailJob throws.
            throw new InvalidOperationException(
                string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }
}
