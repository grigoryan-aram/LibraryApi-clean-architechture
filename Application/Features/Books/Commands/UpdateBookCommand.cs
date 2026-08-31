using Application.DTOs;
using ErrorOr;
using MediatR;

namespace Application.Features.Books.Commands
{
    public record UpdateBookCommand(
        int Id,
        string Title,
        string Author,
        int CategoryId,
        int TotalCopies) :
        IRequest<ErrorOr<BooksDTO>>;

}
