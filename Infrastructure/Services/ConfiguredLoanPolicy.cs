using Application.ServiceInterfaces;
using Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services
{
    public class ConfiguredLoanPolicy : ILoanPolicy
    {
        private const int FallbackPeriodDays = 14;

        public int LoanPeriodDays { get; }
        public ConfiguredLoanPolicy(IOptions<LoanSettings> settings)
        {
            var configured = settings.Value.LoanPeriodDays;

            // A zero or negative period would make every loan overdue the
            // moment it was created — almost certainly a typo in config
            // rather than a policy, so fall back rather than honour it.
            LoanPeriodDays = configured > 0 ? configured : FallbackPeriodDays;
        }



        public DateTime DueDateFor(DateTime borrowedAt) =>
            borrowedAt.AddDays(LoanPeriodDays);
    }
}
