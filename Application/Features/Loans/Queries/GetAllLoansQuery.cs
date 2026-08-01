using ErrorOr;
using LibraryApi.Application.DTOs;
using MediatR;

namespace Application.Features.Loans.Queries
{
    public record GetAllLoansQuery : IRequest<ErrorOr<IReadOnlyList<LoansDTO>>>;

}
