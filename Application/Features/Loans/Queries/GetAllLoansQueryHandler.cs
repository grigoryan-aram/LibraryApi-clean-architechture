using ErrorOr;
using LibraryApi.Application.DTOs;
using LibraryApi.Application.RepositoryInterfaces;
using Mapster;
using MediatR;
namespace Application.Features.Loans.Queries
{
    public class GetAllLoansQueryHandler : IRequestHandler<GetAllLoansQuery, ErrorOr<IReadOnlyList<LoansDTO>>>
    {


        private readonly ILoansRepository _loansRepository;

        public GetAllLoansQueryHandler(ILoansRepository loansRepository)
        {
            _loansRepository = loansRepository;
        }

        public async Task<ErrorOr<IReadOnlyList<LoansDTO>>> Handle(GetAllLoansQuery request, CancellationToken cancellationToken)
        {

            var loans = await _loansRepository.GetAllLoansAsync(cancellationToken);

            if (loans == null)
            {

                return Error.NotFound("Loans.NotFound", "No loans found.");

            }

            return ErrorOrFactory.From(loans.Adapt<IReadOnlyList<LoansDTO>>());

        }
    }
}
