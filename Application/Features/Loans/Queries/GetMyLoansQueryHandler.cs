using Application.DTOs;
using Application.RepositoryInterfaces;
using ErrorOr;
using LibraryApi.Application.RepositoryInterfaces;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Loans.Queries
{
    public class GetMyLoansQueryHandler
        : IRequestHandler<GetMyLoansQuery, ErrorOr<IReadOnlyList<LoansDTO>>>
    {
        private readonly ILoansRepository _loansRepository;
        private readonly IMembersRepository _membersRepository;
        private readonly ILogger<GetMyLoansQueryHandler> _logger;

        public GetMyLoansQueryHandler(
            ILoansRepository loansRepository,
            IMembersRepository membersRepository,
            ILogger<GetMyLoansQueryHandler> logger)
        {
            _loansRepository = loansRepository;
            _membersRepository = membersRepository;
            _logger = logger;
        }

        public async Task<ErrorOr<IReadOnlyList<LoansDTO>>> Handle(
            GetMyLoansQuery request,
            CancellationToken cancellationToken)
        {
            var member = await _membersRepository.GetMemberByIdentityUserIdAsync(
                request.IdentityUserId,
                cancellationToken);

            if (member is null)
            {
                _logger.LogWarning(
                    "Account {IdentityUserId} is not linked to a library member.",
                    request.IdentityUserId);

                return Error.NotFound(
                    "Loans.NoMemberForAccount",
                    "This account is not linked to a library member, so it " +
                    "cannot borrow and has no loans.");
            }

            var loans = await _loansRepository.GetLoansForMemberAsync(
                member.Id,
                cancellationToken);

            var loansDTO = loans.Adapt<IReadOnlyList<LoansDTO>>();

            _logger.LogInformation(
                "Returned {LoanCount} loans for member {MemberId}.",
                loansDTO.Count,
                member.Id);

            return ErrorOrFactory.From(loansDTO);
        }
    }
}
