using Application.Jobs;
using Application.ServiceInterfaces;
using ErrorOr;
using Moq;

namespace Application.UnitTests.Jobs;

// Same contract as SendWelcomeEmailJob: pass everything through, and throw on
// a failed send because that is the only thing Hangfire retries.
public class ReceiptEmailJobTests
{
    private readonly Mock<IEmailService> _emailService = new();

    private static readonly DateTime DueAt = new(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc);

    private static readonly ErrorOr<Success> SendFailed = Error.Failure(
        "Email.SendFailed", "Failed to send a loan receipt to ada@example.com: refused");

    [Fact]
    public async Task Loan_receipt_passes_the_book_and_due_date_through()
    {
        _emailService.Setup(s => s.SendLoanReceiptEmailAsync(
                         It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                     .ReturnsAsync(Result.Success);

        await new SendLoanReceiptEmailJob(_emailService.Object)
            .ExecuteAsync("ada@example.com", "ada", "Dune", DueAt);

        _emailService.Verify(s => s.SendLoanReceiptEmailAsync(
            "ada@example.com", "ada", "Dune", DueAt), Times.Once);
    }

    [Fact]
    public async Task Loan_receipt_throws_when_the_send_failed_so_hangfire_retries()
    {
        _emailService.Setup(s => s.SendLoanReceiptEmailAsync(
                         It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                     .ReturnsAsync(SendFailed);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SendLoanReceiptEmailJob(_emailService.Object)
                .ExecuteAsync("ada@example.com", "ada", "Dune", DueAt));

        Assert.Contains("ada@example.com", thrown.Message);
    }

    [Fact]
    public async Task Return_receipt_passes_lateness_through()
    {
        _emailService.Setup(s => s.SendReturnReceiptEmailAsync(
                         It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                         It.IsAny<DateTime>(), It.IsAny<bool>()))
                     .ReturnsAsync(Result.Success);

        await new SendReturnReceiptEmailJob(_emailService.Object)
            .ExecuteAsync("ada@example.com", "ada", "Dune", DueAt, wasOverdue: true);

        _emailService.Verify(s => s.SendReturnReceiptEmailAsync(
            "ada@example.com", "ada", "Dune", DueAt, true), Times.Once);
    }

    [Fact]
    public async Task Return_receipt_throws_when_the_send_failed_so_hangfire_retries()
    {
        _emailService.Setup(s => s.SendReturnReceiptEmailAsync(
                         It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                         It.IsAny<DateTime>(), It.IsAny<bool>()))
                     .ReturnsAsync(SendFailed);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SendReturnReceiptEmailJob(_emailService.Object)
                .ExecuteAsync("ada@example.com", "ada", "Dune", DueAt, wasOverdue: false));
    }
}
