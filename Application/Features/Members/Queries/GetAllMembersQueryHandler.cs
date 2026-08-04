using Application.DTOs;
using ErrorOr;
using LibraryApi.Application.RepositoryInterfaces;
using Mapster;
using MediatR;

namespace Application.Features.Members.Queries
{
    public class GetAllMembersQueryHandler : IRequestHandler<GetAllMembersQuery, ErrorOr<IReadOnlyList<MembersDTO>>>
    {
        private readonly IMembersRepository _membersRepository;

        public GetAllMembersQueryHandler(IMembersRepository membersRepository)
        {
            _membersRepository = membersRepository;
        }

        public async Task<ErrorOr<IReadOnlyList<MembersDTO>>> Handle(GetAllMembersQuery request, CancellationToken cancellationToken)
        {
            var members = await _membersRepository.GetMembersAsync(cancellationToken);

            if (members == null)
            {

                return Error.Failure("Members.NotFound", "No members found.");
            }
            return ErrorOrFactory.From(members.Adapt<IReadOnlyList<MembersDTO>>());
        }
    }
}
