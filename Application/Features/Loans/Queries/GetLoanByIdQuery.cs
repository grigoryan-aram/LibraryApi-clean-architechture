using ErrorOr;
using LibraryApi.Application.DTOs;
using MediatR;

namespace Application.Features.Loans.Queries
{

    public record GetLoanByIdQuery(int Id) : IRequest<ErrorOr<LoansDTO>>;


}
