using Application.DTOs;
using ErrorOr;

namespace Application.ServiceInterfaces
{
    public interface IClaudeService
    {
        Task<ErrorOr<ClaudeReplyDTO>> AskAsync(
            IReadOnlyList<ChatMessageDTO> conversation,
            CancellationToken cancellationToken);
    }
}
