using Application.RepositoryInterfaces;
using ErrorOr;
using MediatR;
namespace Application.Features.Loans.Commands
{
    public class DeleteLoanCommandHandler : IRequestHandler<DeleteLoanCommand, ErrorOr<Deleted>>
    {

        private readonly ILoansRepository _loansRepository;

        public DeleteLoanCommandHandler(ILoansRepository loansRepository)
        {
            _loansRepository = loansRepository;
        }

        public async Task<ErrorOr<Deleted>> Handle(DeleteLoanCommand request, CancellationToken cancellationToken)
        {


            await _loansRepository.DeleteLoanAsync(request.Id, cancellationToken);

            return Result.Deleted;
        }
    }
}
