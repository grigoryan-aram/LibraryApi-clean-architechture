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
            // The one place in this codebase that throws on purpose, and it is
            // not control flow — it is the only vocabulary Hangfire has.
            //
            // Hangfire decides a job's fate by whether the method threw: a job
            // that RETURNS is recorded as Succeeded and never retried, and a
            // returned ErrorOr is still a return. Handing the error back
            // quietly here would put a lost email in the succeeded list, which
            // is precisely the failure this check exists to prevent.
            //
            // So the result pattern stops at this boundary and becomes the
            // signal the scheduler understands. Everything upstream of here —
            // IEmailService and its implementation — returns ErrorOr.
            throw new InvalidOperationException(
                string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }
}
