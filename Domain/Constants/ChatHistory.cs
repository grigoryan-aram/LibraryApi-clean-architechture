namespace LibraryApi.Domain.Constants;

/// <summary>
/// Limits on the Claude chat history kept between requests.
/// </summary>
public static class ChatHistory
{
    /// <summary>
    /// How many messages of a conversation survive into the next request.
    /// Every stored turn is resent to Claude on the following call, so an
    /// unbounded history means an unbounded bill. Oldest turns fall off
    /// first. Raising this is a billing decision, not a tuning one.
    /// </summary>
    public const int MaxMessagesKept = 20;
}
