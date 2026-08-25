namespace Infrastructure.Settings
{
    public class ClaudeSettings
    {
        // Left empty in appsettings.json on purpose. Supply it through user
        // secrets (dotnet user-secrets set "Claude:ApiKey" "sk-ant-...") or the
        // ANTHROPIC_API_KEY environment variable, which DependencyInjection
        // falls back to. Never commit a key here.
        public string ApiKey { get; set; } = string.Empty;

        public string Model { get; set; } = "claude-opus-5";

        public int MaxTokens { get; set; } = 16000;

        // One message per caller per this many hours. 0 disables the cap.
        public int RateLimitHours { get; set; } = 24;

        public string SystemPrompt { get; set; } =
            "You are the assistant embedded in a library management API. " +
            "Answer questions about books, categories, members and loans, and " +
            "about how to use this API. Keep answers short and concrete.";
    }
}
