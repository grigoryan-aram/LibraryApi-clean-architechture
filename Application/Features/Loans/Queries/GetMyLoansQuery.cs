using Application.DTOs;
using ErrorOr;
using MediatR;

namespace Application.Features.Loans.Queries
{
    public record GetMyLoansQuery(string IdentityUserId)
        : IRequest<ErrorOr<IReadOnlyList<LoansDTO>>>;

}
