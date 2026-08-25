using Application.ServiceInterfaces;
using Infrastructure.Services;
using Infrastructure.Settings;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Infrastructure.UnitTests.Services;

public class InMemoryAiUsageLimiterTests
{
    private static InMemoryAiUsageLimiter CreateSut(int rateLimitHours = 24) =>
        new(new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new ClaudeSettings { RateLimitHours = rateLimitHours }));

    [Fact]
    public void Allows_the_first_message_from_a_caller()
    {
        var decision = CreateSut().Check("ada");

        Assert.True(decision.Allowed);
        Assert.Equal(TimeSpan.Zero, decision.RetryAfter);
    }

    [Fact]
    public void Refuses_a_second_message_inside_the_window()
    {
        var sut = CreateSut();
        sut.Record("ada");

        var decision = sut.Check("ada");

        Assert.False(decision.Allowed);
        Assert.InRange(decision.RetryAfter, TimeSpan.FromHours(23), TimeSpan.FromHours(24));
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

    [Fact]
    public void Defaults_to_a_24_hour_window()
    {
        Assert.Equal(24, new ClaudeSettings().RateLimitHours);
    }
}
