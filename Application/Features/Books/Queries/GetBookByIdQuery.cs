using Application.DTOs;
using ErrorOr;
using MediatR;

namespace Application.Features.Books.Queries
{
    public record GetBookByIdQuery(int id)
          : IRequest<ErrorOr<BooksDTO>>;

}
