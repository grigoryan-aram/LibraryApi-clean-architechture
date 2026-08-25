namespace Application.DTOs
{
    // The wire values Claude expects for ChatMessageDTO.Role. Kept as constants
    // so a typo here is a compile error rather than a rejected API request.
    public static class ChatRoles
    {
        public const string User = "user";
        public const string Assistant = "assistant";
    }
}
