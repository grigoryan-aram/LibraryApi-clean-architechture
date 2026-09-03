using ErrorOr;
using LibraryApi.Application.RepositoryInterfaces;
using MediatR;
using Microsoft.Extensions.Logging;
namespace Application.Features.Members.Commands
{
    public class DeleteMemberCommandHandler : IRequestHandler<DeleteMemberCommand, ErrorOr<Deleted>>
    {

        private readonly IMembersRepository _membersRepository;
        private readonly ILogger<DeleteMemberCommandHandler> _logger;

        public DeleteMemberCommandHandler(
            IMembersRepository membersRepository,
            ILogger<DeleteMemberCommandHandler> logger)
        {
            _membersRepository = membersRepository;
            _logger = logger;
        }



        public async Task<ErrorOr<Deleted>> Handle(DeleteMemberCommand request, CancellationToken cancellationToken)
        {

            await _membersRepository.DeleteMemberAsync(request.Id, cancellationToken);

            _logger.LogInformation("Deleted member {MemberId}.", request.Id);

            return Result.Deleted;
        }
    }
}
