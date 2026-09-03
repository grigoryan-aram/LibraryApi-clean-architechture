using Application.DTOs;
using Application.RepositoryInterfaces;
using ErrorOr;
using LibraryApi.Domain.Entities;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;
namespace Application.Features.Loans.Queries
{
    public class GetLoanByIdQueryHandler : IRequestHandler<GetLoanByIdQuery, ErrorOr<LoansDTO>>
    {

        private readonly ILoansRepository _loansRepository;
        private readonly ILogger<GetLoanByIdQueryHandler> _logger;

        public GetLoanByIdQueryHandler(
            ILoansRepository loansRepository,
            ILogger<GetLoanByIdQueryHandler> logger)
        {
            _loansRepository = loansRepository;
            _logger = logger;
        }



        public async Task<ErrorOr<LoansDTO>> Handle(GetLoanByIdQuery request, CancellationToken cancellationToken)
        {

            var loan = request.Adapt<LoanModel>();

            var result = await _loansRepository.GetLoanByIdAsync(loan.Id, cancellationToken);

            if (result == null)
            {
                _logger.LogWarning("No loan with id {LoanId}.", loan.Id);

                return Error.NotFound("loan not found", "an error has occurred");
            }

            _logger.LogInformation(
                "Returned loan {LoanId} (book {BookId}, member {MemberId}).",
                result.Id,
                result.BookId,
                result.MemberId);

            return result.Adapt<LoansDTO>();


        }
    }
}
