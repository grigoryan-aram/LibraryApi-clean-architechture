namespace Application.DTOs
{
    public record ClaudeChatDTO(
        Guid ConversationId,
        string Reply,
        string Model,
        long InputTokens,
        long OutputTokens,
        IReadOnlyList<ChatMessageDTO> History);
}
