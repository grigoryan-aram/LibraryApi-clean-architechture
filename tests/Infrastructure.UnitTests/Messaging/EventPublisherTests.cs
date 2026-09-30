using Application.IntegrationEvents;
using ErrorOr;
using Infrastructure.Messaging;
using Infrastructure.Settings;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Infrastructure.UnitTests.Messaging;

public class MassTransitEventPublisherTests
{
    private readonly Mock<IPublishEndpoint> _endpoint = new();

    private MassTransitEventPublisher CreateSut() =>
        new(_endpoint.Object, NullLogger<MassTransitEventPublisher>.Instance);

    private static MemberRegistered Event =>
        new("ada", "ada@example.com", 7, DateTime.UtcNow);

    [Fact]
    public async Task Publishes_the_event_to_the_bus()
    {
        var result = await CreateSut().PublishAsync(Event, CancellationToken.None);

        Assert.False(result.IsError);
        _endpoint.Verify(e => e.Publish(
            It.Is<MemberRegistered>(evt => evt.MemberId == 7),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // MassTransit throws when the broker is unreachable. Mapped at this
    // boundary rather than rethrown, the way ClaudeService maps the Anthropic
    // SDK — a thrown exception here would reach GlobalExceptionMiddleware and
    // turn a committed loan into a 500.
    [Fact]
    public async Task Maps_a_broker_failure_to_an_error_instead_of_throwing()
    {
        _endpoint.Setup(e => e.Publish(
                     It.IsAny<MemberRegistered>(), It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new RabbitMqConnectionException("connection refused"));

        var result = await CreateSut().PublishAsync(Event, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Failure, result.FirstError.Type);
        Assert.Equal("Events.PublishFailed", result.FirstError.Code);
    }

    // Publish does not fail fast against an unreachable broker — it waits for
    // the bus to connect. Unbounded, that hangs the HTTP request that caused
    // it, which is exactly what happened before the timeout was added.
    [Fact]
    public async Task Gives_up_rather_than_waiting_forever_for_a_silent_broker()
    {
        _endpoint.Setup(e => e.Publish(
                     It.IsAny<MemberRegistered>(), It.IsAny<CancellationToken>()))
                 .Returns((MemberRegistered _, CancellationToken token) =>
                     Task.Delay(Timeout.Infinite, token));

        var started = DateTime.UtcNow;
        var result = await CreateSut().PublishAsync(Event, CancellationToken.None);
        var elapsed = DateTime.UtcNow - started;

        Assert.True(result.IsError);
        Assert.Equal("Events.PublishTimedOut", result.FirstError.Code);
        Assert.True(elapsed < TimeSpan.FromSeconds(30), $"took {elapsed}");
    }
}

public class NoOpEventPublisherTests
{
    [Fact]
    public async Task Reports_success_so_a_broker_less_deployment_is_unaffected()
    {
        var publisher = new NoOpEventPublisher(NullLogger<NoOpEventPublisher>.Instance);

        var result = await publisher.PublishAsync(
            new MemberRegistered("ada", "ada@example.com", 7, DateTime.UtcNow),
            CancellationToken.None);

        Assert.False(result.IsError);
    }
}

public class RabbitMqSettingsTests
{
    // IsConfigured is what decides whether the bus is registered at all. A
    // half-filled section must count as absent, or MassTransit starts a hosted
    // service that retries a connection it can never make.
    [Theory]
    [InlineData("", "guest", "guest")]
    [InlineData("localhost", "", "guest")]
    [InlineData("localhost", "guest", "")]
    [InlineData("   ", "guest", "guest")]
    public void Is_not_configured_when_any_part_is_missing(
        string host, string username, string password)
    {
        var settings = new RabbitMqSettings
        {
            Host = host,
            Username = username,
            Password = password
        };

        Assert.False(settings.IsConfigured);
    }

    [Fact]
    public void Is_configured_when_host_and_credentials_are_all_present()
    {
        var settings = new RabbitMqSettings
        {
            Host = "localhost",
            Username = "guest",
            Password = "guest"
        };

        Assert.True(settings.IsConfigured);
    }

    [Fact]
    public void Defaults_to_the_standard_amqp_port_and_root_vhost()
    {
        var settings = new RabbitMqSettings();

        Assert.Equal(5672, settings.Port);
        Assert.Equal("/", settings.VirtualHost);
    }
}
