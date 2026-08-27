using Application.Jobs;
using Application.ServiceInterfaces;
using ErrorOr;
using Moq;

namespace Application.UnitTests.Jobs;

public class SendWelcomeEmailJobTests
{
    private readonly Mock<IEmailService> _emailService = new();

    private SendWelcomeEmailJob CreateSut() => new(_emailService.Object);

    private void GivenSendReturns(ErrorOr<Success> result) =>
        _emailService.Setup(service => service.SendWelcomeEmailAsync(
                          It.IsAny<string>(), It.IsAny<string>()))
                     .ReturnsAsync(result);

    [Fact]
    public async Task Passes_the_recipient_and_name_through()
    {
        GivenSendReturns(Result.Success);

        await CreateSut().ExecuteAsync("ada@example.com", "ada");

        _emailService.Verify(service => service.SendWelcomeEmailAsync(
            "ada@example.com", "ada"), Times.Once);
    }

    [Fact]
    public async Task Completes_quietly_when_the_email_went_out()
    {
        GivenSendReturns(Result.Success);

        await CreateSut().ExecuteAsync("ada@example.com", "ada");
    }

    // The point of the whole arrangement. IEmailService returns its failure
    // rather than throwing, but Hangfire reads a job that RETURNS as
    // Succeeded — a returned ErrorOr included. If this job swallowed the error,
    // a lost email would sit in the succeeded list and never be retried, which
    // is the exact bug the send check was written to prevent. So the result
    // pattern stops here and becomes the signal the scheduler understands.
    [Fact]
    public async Task Throws_when_the_send_failed_so_hangfire_records_a_failure()
    {
        GivenSendReturns(Error.Failure(
            "Email.SendFailed",
            "Failed to send welcome email to ada@example.com: connection refused"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateSut().ExecuteAsync("ada@example.com", "ada"));

        Assert.Contains("ada@example.com", exception.Message);
        Assert.Contains("connection refused", exception.Message);
    }
}
