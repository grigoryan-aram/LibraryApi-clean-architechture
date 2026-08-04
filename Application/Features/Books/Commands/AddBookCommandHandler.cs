using Application.DTOs;
using Application.RepositoryInterfaces;
using ErrorOr;
using LibraryApi.Domain.Entities;
using Mapster;
using MediatR;


namespace Application.Features.Books.Commands
{
    public class AddBookCommandHandler : IRequestHandler<AddBookCommand, ErrorOr<BooksDTO>>
    {
        private readonly IBooksRepository _booksRepository;

        public AddBookCommandHandler(IBooksRepository booksRepository)
        {
            _booksRepository = booksRepository;
        }



        public async Task<ErrorOr<BooksDTO>> Handle(
        AddBookCommand request,
        CancellationToken cancellationToken)
        {
            var book = await _booksRepository.AddAsync(
                request.Adapt<BookModel>(),
                cancellationToken);

            if (book == null)
            {

                return Error.Failure("Failed to add book");

            }

            return book.Adapt<BooksDTO>();
        }
    }
}

