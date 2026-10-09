using ErrorOr;
using FluentEmail.Core;
using FluentEmail.Core.Models;
using Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Infrastructure.UnitTests.Services;

public class EmailServiceTests
{
    private readonly Mock<IFluentEmail> _email = new();

    public EmailServiceTests()
    {
        // IFluentEmail is a fluent builder, so every step has to hand the same
        // mock back or the chain returns null halfway through.
        //
        // Match the overload the code actually calls: IFluentEmail declares
        // To(string), To(string, string) and To(IEnumerable<Address>), and
        // EmailService calls the single-argument one. Setting up a sibling
        // overload leaves the real call returning Moq's default null, which
        // surfaces as a NullReferenceException inside the chain.
        _email.Setup(e => e.To(It.IsAny<string>()))
              .Returns(() => _email.Object);
        _email.Setup(e => e.Subject(It.IsAny<string>()))
              .Returns(() => _email.Object);
        _email.Setup(e => e.Body(It.IsAny<string>(), It.IsAny<bool>()))
              .Returns(() => _email.Object);
    }

    private void GivenSendReturns(SendResponse response) =>
        _email.Setup(e => e.SendAsync(It.IsAny<CancellationToken?>()))
              .ReturnsAsync(response);

    private EmailService CreateSut() =>
        new(_email.Object, NullLogger<EmailService>.Instance);

    [Fact]
    public async Task Reports_success_when_the_send_succeeds()
    {
        GivenSendReturns(new SendResponse());

        var result = await CreateSut().SendWelcomeEmailAsync("ada@example.com", "ada");

        Assert.False(result.IsError);
        _email.Verify(e => e.SendAsync(It.IsAny<CancellationToken?>()), Times.Once);
    }

    // FluentEmail reports SMTP failures on the response instead of throwing.
    // Before this check existed, a failed send was indistinguishable from a
    // successful one — which is how a lost email was recorded as a *succeeded*
    // Hangfire job. This service now returns the failure rather than throwing
    // it; turning that into a retry is SendWelcomeEmailJob's job, and
    // SendWelcomeEmailJobTests covers that half.
    [Fact]
    public async Task Returns_an_error_when_the_send_fails()
    {
        GivenSendReturns(new SendResponse
        {
            ErrorMessages = { "target machine actively refused it" }
        });

        var result = await CreateSut().SendWelcomeEmailAsync("ada@example.com", "ada");

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Failure, result.FirstError.Type);
        Assert.Equal("Email.SendFailed", result.FirstError.Code);
        Assert.Contains("ada@example.com", result.FirstError.Description);
        Assert.Contains("target machine actively refused it", result.FirstError.Description);
    }

    // The promise is "this does not throw", so an exception escaping the
    // third-party builder has to become an error too, not a 500.
    [Fact]
    public async Task Returns_an_error_when_the_underlying_sender_throws()
    {
        _email.Setup(e => e.SendAsync(It.IsAny<CancellationToken?>()))
              .ThrowsAsync(new InvalidOperationException("socket exploded"));

        var result = await CreateSut().SendWelcomeEmailAsync("ada@example.com", "ada");

        Assert.True(result.IsError);
        Assert.Equal("Email.SendFailed", result.FirstError.Code);
        Assert.Contains("socket exploded", result.FirstError.Description);
    }

    [Fact]
    public async Task Addresses_the_email_to_the_requested_recipient()
    {
        GivenSendReturns(new SendResponse());

        _ = await CreateSut().SendWelcomeEmailAsync("ada@example.com", "ada");

        _email.Verify(e => e.To("ada@example.com"), Times.Once);
    }

    [Fact]
    public async Task Loan_receipt_names_the_book_and_the_due_date()
    {
        GivenSendReturns(new SendResponse());

        var result = await CreateSut().SendLoanReceiptEmailAsync(
            "ada@example.com", "ada", "Dune", new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc));

        Assert.False(result.IsError);
        _email.Verify(e => e.Subject("You borrowed \"Dune\""), Times.Once);
        _email.Verify(e => e.Body(
            It.Is<string>(body => body.Contains("Thursday 22 October 2026")),
            It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async Task Return_receipt_says_so_when_the_book_came_back_late()
    {
        GivenSendReturns(new SendResponse());

        await CreateSut().SendReturnReceiptEmailAsync(
            "ada@example.com", "ada", "Dune", DateTime.UtcNow, wasOverdue: true);

        _email.Verify(e => e.Body(
            It.Is<string>(body => body.Contains("after its due date")),
            It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async Task Receipts_report_a_failed_send_like_every_other_email()
    {
        GivenSendReturns(new SendResponse { ErrorMessages = { "mailbox unavailable" } });

        var result = await CreateSut().SendReturnReceiptEmailAsync(
            "ada@example.com", "ada", "Dune", DateTime.UtcNow, wasOverdue: false);

        Assert.True(result.IsError);
        Assert.Equal("Email.SendFailed", result.FirstError.Code);
        Assert.Contains("mailbox unavailable", result.FirstError.Description);
    }
}
