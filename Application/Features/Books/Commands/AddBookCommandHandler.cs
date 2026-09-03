using Application.DTOs;
using Application.RepositoryInterfaces;
using ErrorOr;
using LibraryApi.Domain.Entities;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Books.Commands
{
    public class AddBookCommandHandler : IRequestHandler<AddBookCommand, ErrorOr<BooksDTO>>
    {
        private readonly IBooksRepository _booksRepository;
        private readonly ICategorysRepository _categorysRepository;
        private readonly ILogger<AddBookCommandHandler> _logger;

        public AddBookCommandHandler(
            IBooksRepository booksRepository,
            ICategorysRepository categorysRepository,
            ILogger<AddBookCommandHandler> logger)
        {
            _booksRepository = booksRepository;
            _categorysRepository = categorysRepository;
            _logger = logger;
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
                _logger.LogWarning(
                    "Rejected adding book {Title}: no category with id {CategoryId}.",
                    request.Title,
                    request.CategoryId);

                return Error.NotFound(
                    "Books.CategoryNotFound",
                    $"No category with id {request.CategoryId}.");
            }

            var book = await _booksRepository.AddAsync(
                request.Adapt<BookModel>(),
                cancellationToken);

            if (book == null)
            {
                _logger.LogError(
                    "The books repository returned no row when adding {Title}.",
                    request.Title);

                return Error.Failure("Failed to add book");
            }

            _logger.LogInformation(
                "Added book {BookId} ({Title}) in category {CategoryId}.",
                book.Id,
                book.Title,
                book.CategoryId);

            return book.Adapt<BooksDTO>();
        }
    }
}
