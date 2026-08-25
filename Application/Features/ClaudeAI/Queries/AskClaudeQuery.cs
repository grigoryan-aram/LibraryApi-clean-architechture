using Application.DTOs;
using ErrorOr;
using MediatR;

namespace Application.Features.ClaudeAI.Queries
{
    // Requester is never bound from the request — the controller takes it from
    // User.Identity and the Blazor page from its authentication state, so a
    // caller cannot hand themselves someone else's allowance.
    public record AskClaudeQuery(
        string Message,
        Guid? ConversationId,
        string Requester
    ) : IRequest<ErrorOr<ClaudeChatDTO>>;
}
