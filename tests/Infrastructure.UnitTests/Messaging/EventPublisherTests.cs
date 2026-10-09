using Application.IntegrationEvents;
using Application.ServiceInterfaces;
using ErrorOr;
using Infrastructure.Messaging;
using Infrastructure.Repositories;
using Infrastructure.Settings;
using Infrastructure.UnitTests.Repositories;
using LibraryApi.Domain.Entities;
using LibraryApi.Infrastructure.Data;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Infrastructure.UnitTests.Messaging;

public class OutboxEventPublisherTests
{
    // The publish endpoint is MassTransit's to get right; this is about the
    // boundary. A thrown exception here would bypass the caller's rollback and
    // land in GlobalExceptionMiddleware.
    [Fact]
    public async Task Maps_a_failure_to_record_the_event_to_an_unexpected_error()
    {
        using var db = new SqliteDatabase();
        var endpoint = new Mock<IPublishEndpoint>();
        endpoint.Setup(e => e.Publish(It.IsAny<MemberRegistered>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("outbox table is missing"));

        var result = await new OutboxEventPublisher(
                endpoint.Object, db.Context, NullLogger<OutboxEventPublisher>.Instance)
            .PublishAsync(
                new MemberRegistered("ada", "ada@example.com", 7, DateTime.UtcNow),
                CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Unexpected, result.FirstError.Type);
        Assert.Equal("Events.NotRecorded", result.FirstError.Code);
    }
}

/// <summary>
/// The outbox for real: MassTransit's bus outbox over SQLite, no broker.
/// </summary>
/// <remarks>
/// The claim worth proving is the one the whole arrangement exists for — an
/// entity and the event announcing it reach the database together or not at
/// all, and recording an event never needs RabbitMQ to be there. The delivery
/// service is disabled so the rows stay put to be counted.
/// </remarks>
public sealed class TransactionalOutboxTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ServiceProvider _provider;

    public TransactionalOutboxTests()
    {
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<LibraryDBContext>(options => options.UseSqlite(_connection));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IEventPublisher, OutboxEventPublisher>();
        services.AddMassTransit(bus =>
        {
            bus.AddEntityFrameworkOutbox<LibraryDBContext>(outbox =>
            {
                outbox.UseSqlite();
                outbox.UseBusOutbox(o => o.DisableDeliveryService());
            });

            bus.UsingInMemory();
        });

        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<LibraryDBContext>().Database.EnsureCreated();
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task<ErrorOr<int>> AddMemberAndAnnounce(bool thenFail)
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<LibraryDBContext>();
        var events = scope.ServiceProvider.GetRequiredService<IEventPublisher>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        return await unitOfWork.ExecuteInTransactionAsync<int>(async token =>
        {
            var member = new MemberModel { Name = "ada" };
            context.Members.Add(member);
            await context.SaveChangesAsync(token);

            var published = await events.PublishAsync(
                new MemberRegistered("ada", "ada@example.com", member.Id, DateTime.UtcNow),
                token);

            if (published.IsError)
            {
                return published.Errors;
            }

            if (thenFail)
            {
                return Error.Failure("Test.Failed", "something after the publish failed");
            }

            return member.Id;
        }, CancellationToken.None);
    }

    private async Task<(int Members, int OutboxMessages)> Rows()
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<LibraryDBContext>();

        return (
            await context.Members.CountAsync(),
            await context.Set<OutboxMessage>().CountAsync());
    }

    [Fact]
    public async Task Commits_the_entity_and_its_event_together_without_a_broker()
    {
        var result = await AddMemberAndAnnounce(thenFail: false);

        Assert.False(result.IsError);
        Assert.Equal((1, 1), await Rows());
    }

    [Fact]
    public async Task Rolls_back_the_event_with_the_entity()
    {
        var result = await AddMemberAndAnnounce(thenFail: true);

        Assert.True(result.IsError);
        Assert.Equal((0, 0), await Rows());
    }

    [Fact]
    public async Task Records_the_event_type_so_the_delivery_service_publishes_the_right_message()
    {
        await AddMemberAndAnnounce(thenFail: false);

        using var scope = _provider.CreateScope();
        var message = await scope.ServiceProvider.GetRequiredService<LibraryDBContext>()
            .Set<OutboxMessage>().SingleAsync();

        Assert.Contains(nameof(MemberRegistered), message.MessageType);
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
