using Application.DTOs;
using ErrorOr;
using MediatR;

namespace Application.Features.Books.Commands
{
    public record AddBookCommand(
            string Title,
            string Author,
            int CategoryId,
            int TotalCopies) :
            IRequest<ErrorOr<BooksDTO>>;



}
