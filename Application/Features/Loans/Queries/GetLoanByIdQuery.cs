using Application.DTOs;
using ErrorOr;
using MediatR;

namespace Application.Features.Loans.Queries
{

    public record GetLoanByIdQuery(int Id)
  : IRequest<ErrorOr<LoansDTO>>;


}
