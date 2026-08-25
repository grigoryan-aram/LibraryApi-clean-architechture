using Application.DTOs;

namespace Application.ServiceInterfaces
{
    // Keeps a conversation alive between requests so the Swagger caller only
    // has to send the next message plus its conversation id.
    public interface IChatHistoryStore
    {
        IReadOnlyList<ChatMessageDTO> Get(Guid conversationId);

        void Save(
            Guid conversationId,
            IReadOnlyList<ChatMessageDTO> conversation);
    }
}
