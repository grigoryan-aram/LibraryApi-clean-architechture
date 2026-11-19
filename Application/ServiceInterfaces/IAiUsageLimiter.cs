namespace Application.ServiceInterfaces
{
    // Disabled means nobody may chat at all, so there is no retry time to give.
    public sealed record AiUsageDecision(
        bool Allowed,
        TimeSpan RetryAfter,
        bool Disabled = false);

    // Caps how often one caller may reach Claude. This lives behind the
    // handler rather than in ASP.NET Core's rate limiter on purpose: the
    // Blazor chat page sends AskClaudeQuery straight to IMediator and never
    // crosses the HTTP pipeline, so an endpoint filter would not see it.
    public interface IAiUsageLimiter
    {
        // Asks whether the caller may spend a message, without spending it.
        AiUsageDecision Check(string requester);

        // Spends it. Called only after Claude actually answered, so a failed
        // turn does not cost the caller their allowance.
        void Record(string requester);
    }
}
