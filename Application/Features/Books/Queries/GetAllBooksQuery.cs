using Application.DTOs;
using ErrorOr;
using MediatR;

namespace Application.Features.Books.Queries
{
    public record GetAllBooksQuery :
     IRequest<ErrorOr<IReadOnlyList<BooksDTO>>>;

}
