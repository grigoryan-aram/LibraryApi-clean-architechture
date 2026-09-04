using Application.DTOs;
using Application.ServiceInterfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    // Process-local on purpose: conversations are throwaway Swagger sessions,
    // not library data. Nothing here survives a restart, and a second instance
    // behind a load balancer would not see another instance's conversations.
    public class InMemoryChatHistoryStore : IChatHistoryStore
    {
        private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

        private readonly ILogger<InMemoryChatHistoryStore> _logger;
        private readonly IMemoryCache _cache;

        public InMemoryChatHistoryStore(
            IMemoryCache cache,
            ILogger<InMemoryChatHistoryStore> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public IReadOnlyList<ChatMessageDTO> Get(Guid conversationId)
        {
            if (_cache.TryGetValue(
                    Key(conversationId),
                    out IReadOnlyList<ChatMessageDTO>? conversation)
                && conversation is not null)
            {
                return conversation;
            }

            return Array.Empty<ChatMessageDTO>();
        }

        public void Save(
            Guid conversationId,
            IReadOnlyList<ChatMessageDTO> conversation)
        {
            _logger.LogInformation("Saving chat history for conversation: {ConversationId}", conversationId);

            _cache.Set(
                Key(conversationId),
                conversation,
                new MemoryCacheEntryOptions
                {
                    SlidingExpiration = Lifetime
                });
        }

        private static string Key(Guid conversationId) =>
            $"claude-chat:{conversationId}";
    }
}
