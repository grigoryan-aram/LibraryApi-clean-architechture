using Application.DTOs;
using ErrorOr;
using MediatR;

namespace Application.Features.Loans.Queries
{
    // Open loans whose due date has passed. Filtered in SQL rather than by
    // pulling every loan back and sifting it in memory.
    public record GetOverdueLoansQuery
        : IRequest<ErrorOr<IReadOnlyList<LoansDTO>>>;

}
