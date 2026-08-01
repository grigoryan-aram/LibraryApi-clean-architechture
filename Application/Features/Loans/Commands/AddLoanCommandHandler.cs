using ErrorOr;
using LibraryApi.Application.DTOs;
using LibraryApi.Application.RepositoryInterfaces;
using LibraryApi.Domain.Entities;
using Mapster;
using MediatR;
namespace Application.Features.Loans.Commands
{
    public class AddLoanCommandHandler : IRequestHandler<AddLoanCommand, ErrorOr<LoansDTO>>
    {

        private readonly ILoansRepository _loansRepository;

        public AddLoanCommandHandler(ILoansRepository loansRepository)
        {
            _loansRepository = loansRepository;
        }



        public async Task<ErrorOr<LoansDTO>> Handle(AddLoanCommand request, CancellationToken cancellationToken)
        {
            var loan = request.Adapt<LoanModel>();

            var result = await _loansRepository.AddLoanAsync(loan.BookId, loan.MemberId, cancellationToken);

            if (result == null)
            {

                return Error.Failure("Could not add a loan", "a failure has occurred");
            }



            return result.Adapt<LoansDTO>();
        }
    }
}
