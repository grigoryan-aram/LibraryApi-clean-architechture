using ErrorOr;
using LibraryApi.Application.DTOs;
using MediatR;

namespace Application.Features.Books.Queries
{
    public record GetBookByIdQuery(int id)
          : IRequest<ErrorOr<BooksDTO>>;

}
