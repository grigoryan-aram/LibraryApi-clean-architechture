using Application.DTOs;
using Application.RepositoryInterfaces;
using ErrorOr;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Loans.Queries
{
    public class GetAllLoansQueryHandler : IRequestHandler<GetAllLoansQuery, ErrorOr<IReadOnlyList<LoansDTO>>>
    {


        private readonly ILoansRepository _loansRepository;
        private readonly ILogger<GetAllLoansQueryHandler> _logger;

        public GetAllLoansQueryHandler(
            ILoansRepository loansRepository,
            ILogger<GetAllLoansQueryHandler> logger)
        {
            _loansRepository = loansRepository;
            _logger = logger;
        }

        public async Task<ErrorOr<IReadOnlyList<LoansDTO>>> Handle(GetAllLoansQuery request, CancellationToken cancellationToken)
        {

            var loans = await _loansRepository.GetAllLoansAsync(cancellationToken);

            if (loans == null)
            {
                _logger.LogError("The loans repository returned no collection.");

                return Error.NotFound("Loans.NotFound", "No loans found.");

            }

            var loansDTO = loans.Adapt<IReadOnlyList<LoansDTO>>();

            _logger.LogInformation("Returned {LoanCount} loans.", loansDTO.Count);

            return ErrorOrFactory.From(loansDTO);

        }
    }
}
