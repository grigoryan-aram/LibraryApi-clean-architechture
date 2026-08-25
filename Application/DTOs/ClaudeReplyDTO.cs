namespace Application.DTOs
{
    public record ClaudeReplyDTO(
        string Reply,
        string Model,
        long InputTokens,
        long OutputTokens);
}
