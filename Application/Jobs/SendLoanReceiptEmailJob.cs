using Application.ServiceInterfaces;

namespace Application.Jobs;

public class SendLoanReceiptEmailJob
{
    private readonly IEmailService _emailService;

    public SendLoanReceiptEmailJob(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task ExecuteAsync(
        string email,
        string username,
        string bookTitle,
        DateTime dueAt)
    {
        var result = await _emailService.SendLoanReceiptEmailAsync(
            email,
            username,
            bookTitle,
            dueAt);

        if (result.IsError)
        {
            // Same reason as SendWelcomeEmailJob: Hangfire only retries a job
            // that throws.
            throw new InvalidOperationException(
                string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }
}
