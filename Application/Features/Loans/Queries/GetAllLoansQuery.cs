using Application.DTOs;
using ErrorOr;
using MediatR;

namespace Application.Features.Loans.Queries
{
    public record GetAllLoansQuery
  : IRequest<ErrorOr<IReadOnlyList<LoansDTO>>>;

}
