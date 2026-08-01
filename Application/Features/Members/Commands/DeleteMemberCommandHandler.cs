using ErrorOr;
using LibraryApi.Application.RepositoryInterfaces;
using MediatR;
namespace Application.Features.Members.Commands
{
    public class DeleteMemberCommandHandler : IRequestHandler<DeleteMemberCommand, ErrorOr<Deleted>>
    {

        private readonly IMembersRepository _membersRepository;

        public DeleteMemberCommandHandler(IMembersRepository membersRepository)
        {
            _membersRepository = membersRepository;
        }



        public async Task<ErrorOr<Deleted>> Handle(DeleteMemberCommand request, CancellationToken cancellationToken)
        {

            await _membersRepository.DeleteMemberAsync(request.Id, cancellationToken);

            return Result.Deleted;
        }
    }
}
