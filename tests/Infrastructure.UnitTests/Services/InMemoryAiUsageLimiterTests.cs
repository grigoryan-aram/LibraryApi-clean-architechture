using Application.ServiceInterfaces;
using Infrastructure.Services;
using Infrastructure.Settings;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Infrastructure.UnitTests.Services;

public class InMemoryAiUsageLimiterTests
{
    private static InMemoryAiUsageLimiter CreateSut(
        int rateLimitHours = 24,
        int messagesPerWindow = 1) =>
        new(new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new ClaudeSettings
            {
                RateLimitHours = rateLimitHours,
                MessagesPerWindow = messagesPerWindow
            }),
            NullLogger<InMemoryAiUsageLimiter>.Instance);

    [Fact]
    public void Allows_the_first_message_from_a_caller()
    {
        var decision = CreateSut().Check("ada");

        Assert.True(decision.Allowed);
        Assert.False(decision.Disabled);
        Assert.Equal(TimeSpan.Zero, decision.RetryAfter);
    }

    [Fact]
    public void Refuses_a_second_message_inside_the_window()
    {
        var sut = CreateSut();
        sut.Record("ada");

        var decision = sut.Check("ada");

        Assert.False(decision.Allowed);
        Assert.False(decision.Disabled);
        Assert.InRange(decision.RetryAfter, TimeSpan.FromHours(23), TimeSpan.FromHours(24));
    }

    [Fact]
    public void Allows_as_many_messages_as_configured_and_refuses_the_next()
    {
        var sut = CreateSut(messagesPerWindow: 3);

        for (var i = 0; i < 3; i++)
        {
            Assert.True(sut.Check("ada").Allowed);
            sut.Record("ada");
        }

        Assert.False(sut.Check("ada").Allowed);
    }

    // One caller burning their message must not lock anyone else out.
    [Fact]
    public void Keeps_allowances_separate_per_caller()
    {
        var sut = CreateSut();
        sut.Record("ada");

        Assert.False(sut.Check("ada").Allowed);
        Assert.True(sut.Check("grace").Allowed);
    }

    [Fact]
    public void Allows_everything_when_the_window_is_zero()
    {
        var sut = CreateSut(rateLimitHours: 0);
        sut.Record("ada");

        Assert.True(sut.Check("ada").Allowed);
    }

    // Zero messages wins over a zero window: "no cap" must not reopen an
    // assistant someone deliberately turned off.
    [Theory]
    [InlineData(24)]
    [InlineData(0)]
    public void Refuses_everyone_when_zero_messages_are_allowed(int rateLimitHours)
    {
        var sut = CreateSut(rateLimitHours, messagesPerWindow: 0);

        var decision = sut.Check("ada");

        Assert.False(decision.Allowed);
        Assert.True(decision.Disabled);
        Assert.False(sut.Check("someone-who-never-asked").Allowed);
    }

    [Fact]
    public void Defaults_to_one_message_per_24_hours()
    {
        var settings = new ClaudeSettings();

        Assert.Equal((24, 1), (settings.RateLimitHours, settings.MessagesPerWindow));
    }
}
