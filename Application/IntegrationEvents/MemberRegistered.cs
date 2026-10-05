namespace Application.IntegrationEvents
{
    public record MemberRegistered(
        string Username,
        string Email,
        int MemberId,
        DateTime OccurredAt);
}
