using ErrorOr;
using LibraryApi.Application.DTOs;
using MediatR;

namespace Application.Features.Books.Commands
{
    public record AddBookCommand(
            string Title,
            string Author,
            int CategoryId,
            bool isBorrowed) :
            IRequest<ErrorOr<BooksDTO>>;



}
