using Application.DTOs;
using Application.ServiceInterfaces;
using ErrorOr;
using MediatR;

namespace Application.Features.ClaudeAI.Queries
{
    public class AskClaudeQueryHandler
        : IRequestHandler<AskClaudeQuery, ErrorOr<ClaudeChatDTO>>
    {
        // Every turn is resent to Claude on the next call, so an unbounded
        // history means an unbounded bill. Oldest turns fall off first.
        private const int MaxMessagesKept = 20;

        private readonly IClaudeService _claudeService;
        private readonly IChatHistoryStore _historyStore;
        private readonly IAiUsageLimiter _usageLimiter;

        public AskClaudeQueryHandler(
            IClaudeService claudeService,
            IChatHistoryStore historyStore,
            IAiUsageLimiter usageLimiter)
        {
            _claudeService = claudeService;
            _historyStore = historyStore;
            _usageLimiter = usageLimiter;
        }

        public async Task<ErrorOr<ClaudeChatDTO>> Handle(
            AskClaudeQuery request,
            CancellationToken cancellationToken)
        {
            var allowance = _usageLimiter.Check(request.Requester);

            if (!allowance.Allowed)
            {
                return Error.Failure(
                    "Claude.DailyLimitReached",
                    $"You have used your Claude message for today. " +
                    $"Try again in {Describe(allowance.RetryAfter)}.");
            }

            var conversationId = request.ConversationId ?? Guid.NewGuid();

            var conversation = new List<ChatMessageDTO>(
                _historyStore.Get(conversationId))
            {
                new(ChatRoles.User, request.Message)
            };

            var reply = await _claudeService.AskAsync(
                conversation,
                cancellationToken);

            if (reply.IsError)
            {
                // The failed turn is deliberately not stored, and it does not
                // spend the caller's allowance either: retrying with the same
                // conversation id should not replay a question Claude never
                // answered, nor cost them a message they never got.
                return reply.Errors;
            }

            _usageLimiter.Record(request.Requester);

            // A new list rather than appending to the one just handed to the
            // service: nothing should mutate a collection a collaborator may
            // still be holding.
            var history = Trim(new List<ChatMessageDTO>(conversation)
            {
                new(ChatRoles.Assistant, reply.Value.Reply)
            });

            _historyStore.Save(conversationId, history);

            return new ClaudeChatDTO(
                conversationId,
                reply.Value.Reply,
                reply.Value.Model,
                reply.Value.InputTokens,
                reply.Value.OutputTokens,
                history);
        }

        private static IReadOnlyList<ChatMessageDTO> Trim(
            List<ChatMessageDTO> conversation)
        {
            if (conversation.Count <= MaxMessagesKept)
            {
                return conversation;
            }

            return conversation
                .Skip(conversation.Count - MaxMessagesKept)
                .ToList();
        }

        private static string Describe(TimeSpan retryAfter)
        {
            if (retryAfter.TotalMinutes < 1)
            {
                return "less than a minute";
            }

            if (retryAfter.TotalHours < 1)
            {
                return $"{(int)retryAfter.TotalMinutes} minutes";
            }

            return $"{(int)retryAfter.TotalHours} hours";
        }
    }
}
