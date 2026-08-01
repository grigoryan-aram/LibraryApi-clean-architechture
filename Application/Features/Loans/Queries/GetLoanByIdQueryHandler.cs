using ErrorOr;
using LibraryApi.Application.DTOs;
using LibraryApi.Application.RepositoryInterfaces;
using LibraryApi.Domain.Entities;
using Mapster;
using MediatR;
namespace Application.Features.Loans.Queries
{
    public class GetLoanByIdQueryHandler : IRequestHandler<GetLoanByIdQuery, ErrorOr<LoansDTO>>
    {

        private readonly ILoansRepository _loansRepository;

        public GetLoanByIdQueryHandler(ILoansRepository loansRepository)
        {
            _loansRepository = loansRepository;
        }



        public async Task<ErrorOr<LoansDTO>> Handle(GetLoanByIdQuery request, CancellationToken cancellationToken)
        {

            var loan = request.Adapt<LoanModel>();

            var result = await _loansRepository.GetLoanByIdAsync(loan.Id, cancellationToken);

            if (result == null)
            {

                return Error.NotFound("loan not found", "an error has occurred");
            }

            return result.Adapt<LoansDTO>();


        }
    }
}
