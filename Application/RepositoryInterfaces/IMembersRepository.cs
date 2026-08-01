using LibraryApi.Domain.Entities;

namespace LibraryApi.Application.RepositoryInterfaces
{
    public interface IMembersRepository
    {
        Task<IReadOnlyList<MemberModel>> GetMembersAsync(CancellationToken cancellationToken);
        Task<MemberModel> AddMemberAsync(MemberModel member, CancellationToken cancellationToken);
        Task DeleteMemberAsync(int id, CancellationToken cancellationToken);
        Task<MemberModel?> GetMemberByIdAsync(int id, CancellationToken cancellationToken);



    }
}
