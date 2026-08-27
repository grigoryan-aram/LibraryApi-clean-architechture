using Infrastructure.Services;
using Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Infrastructure.UnitTests.Services;

public class ConfiguredLoanPolicyTests
{
    private static ConfiguredLoanPolicy CreateSut(int? loanPeriodDays = null) =>
        new(Options.Create(loanPeriodDays is null
            ? new LoanSettings()
            : new LoanSettings { LoanPeriodDays = loanPeriodDays.Value }));

    [Fact]
    public void Defaults_to_a_fortnight_when_nothing_is_configured()
    {
        Assert.Equal(14, CreateSut().LoanPeriodDays);
    }

    [Fact]
    public void Uses_the_configured_period()
    {
        var policy = CreateSut(30);

        var borrowedAt = new DateTime(2026, 1, 1, 9, 30, 0, DateTimeKind.Utc);

        Assert.Equal(30, policy.LoanPeriodDays);
        Assert.Equal(borrowedAt.AddDays(30), policy.DueDateFor(borrowedAt));
    }

    // A zero or negative period would make every loan overdue the instant it
    // was created, which is a typo in config rather than a lending policy.
    [Theory]
    [InlineData(0)]
    [InlineData(-7)]
    public void Falls_back_rather_than_honouring_a_period_that_cannot_work(int configured)
    {
        var policy = CreateSut(configured);

        var borrowedAt = DateTime.UtcNow;

        Assert.Equal(14, policy.LoanPeriodDays);
        Assert.True(policy.DueDateFor(borrowedAt) > borrowedAt);
    }

    [Fact]
    public void Keeps_the_time_of_day_when_working_out_the_due_date()
    {
        var borrowedAt = new DateTime(2026, 3, 10, 16, 45, 12, DateTimeKind.Utc);

        Assert.Equal(
            new DateTime(2026, 3, 24, 16, 45, 12, DateTimeKind.Utc),
            CreateSut(14).DueDateFor(borrowedAt));
    }
}
