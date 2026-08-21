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
    public async Task Completes_quietly_when_the_send_succeeds()
    {
        GivenSendReturns(new SendResponse());

        await CreateSut().SendWelcomeEmailAsync("ada@example.com", "ada");

        _email.Verify(e => e.SendAsync(It.IsAny<CancellationToken?>()), Times.Once);
    }

    // FluentEmail reports SMTP failures on the response instead of throwing.
    // Before this behavior existed, a failed send was recorded as a *succeeded*
    // Hangfire job and never retried, so this is the test that keeps the
    // swallow from coming back.
    [Fact]
    public async Task Throws_when_the_send_fails_so_hangfire_can_retry()
    {
        GivenSendReturns(new SendResponse
        {
            ErrorMessages = { "target machine actively refused it" }
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateSut().SendWelcomeEmailAsync("ada@example.com", "ada"));

        Assert.Contains("ada@example.com", ex.Message);
        Assert.Contains("target machine actively refused it", ex.Message);
    }

    [Fact]
    public async Task Addresses_the_email_to_the_requested_recipient()
    {
        GivenSendReturns(new SendResponse());

        await CreateSut().SendWelcomeEmailAsync("ada@example.com", "ada");

        _email.Verify(e => e.To("ada@example.com"), Times.Once);
    }
}
