using Application.ServiceInterfaces;

namespace Application.Jobs;

public class SendReturnReceiptEmailJob
{
    private readonly IEmailService _emailService;

    public SendReturnReceiptEmailJob(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task ExecuteAsync(
        string email,
        string username,
        string bookTitle,
        DateTime returnedAt,
        bool wasOverdue)
    {
        var result = await _emailService.SendReturnReceiptEmailAsync(
            email,
            username,
            bookTitle,
            returnedAt,
            wasOverdue);

        if (result.IsError)
        {
            // Same reason as SendWelcomeEmailJob: Hangfire only retries a job
            // that throws.
            throw new InvalidOperationException(
                string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }
}
