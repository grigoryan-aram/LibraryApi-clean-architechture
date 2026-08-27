using ErrorOr;
using FluentEmail.Core;
using FluentEmail.Core.Models;
using Infrastructure.Services;
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

    private EmailService CreateSut() => new(_email.Object);

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
}
