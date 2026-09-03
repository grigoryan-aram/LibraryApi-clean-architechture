using Application.DTOs;
using ErrorOr;
using LibraryApi.Application.RepositoryInterfaces;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Members.Queries
{
    public class GetAllMembersQueryHandler : IRequestHandler<GetAllMembersQuery, ErrorOr<IReadOnlyList<MembersDTO>>>
    {
        private readonly IMembersRepository _membersRepository;
        private readonly ILogger<GetAllMembersQueryHandler> _logger;

        public GetAllMembersQueryHandler(
            IMembersRepository membersRepository,
            ILogger<GetAllMembersQueryHandler> logger)
        {
            _membersRepository = membersRepository;
            _logger = logger;
        }

        public async Task<ErrorOr<IReadOnlyList<MembersDTO>>> Handle(GetAllMembersQuery request, CancellationToken cancellationToken)
        {
            var members = await _membersRepository.GetMembersAsync(cancellationToken);

            if (members == null)
            {
                _logger.LogError("The members repository returned no collection.");

                return Error.Failure("Members.NotFound", "No members found.");
            }

            var membersDTO = members.Adapt<IReadOnlyList<MembersDTO>>();

            _logger.LogInformation("Returned {MemberCount} members.", membersDTO.Count);

            return ErrorOrFactory.From(membersDTO);
        }
    }
}
