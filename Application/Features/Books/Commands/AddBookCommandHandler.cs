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
        private readonly ICategorysRepository _categorysRepository;

        public AddBookCommandHandler(
            IBooksRepository booksRepository,
            ICategorysRepository categorysRepository)
        {
            _booksRepository = booksRepository;
            _categorysRepository = categorysRepository;
        }



        public async Task<ErrorOr<BooksDTO>> Handle(
        AddBookCommand request,
        CancellationToken cancellationToken)
        {
            var category = await _categorysRepository.GetCategoryByIdAsync(
                request.CategoryId,
                cancellationToken);

            if (category is null)
            {
                return Error.NotFound(
                    "Books.CategoryNotFound",
                    $"No category with id {request.CategoryId}.");
            }

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
