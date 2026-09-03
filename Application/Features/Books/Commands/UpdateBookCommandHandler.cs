using Application.DTOs;
using Application.RepositoryInterfaces;
using ErrorOr;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Books.Commands
{
    public class UpdateBookCommandHandler
        : IRequestHandler<UpdateBookCommand, ErrorOr<BooksDTO>>
    {
        private readonly IBooksRepository _booksRepository;
        private readonly ICategorysRepository _categorysRepository;
        private readonly ILoansRepository _loansRepository;
        private readonly ILogger<UpdateBookCommandHandler> _logger;

        public UpdateBookCommandHandler(
            IBooksRepository booksRepository,
            ICategorysRepository categorysRepository,
            ILoansRepository loansRepository,
            ILogger<UpdateBookCommandHandler> logger)
        {
            _booksRepository = booksRepository;
            _categorysRepository = categorysRepository;
            _loansRepository = loansRepository;
            _logger = logger;
        }

        public async Task<ErrorOr<BooksDTO>> Handle(
            UpdateBookCommand request,
            CancellationToken cancellationToken)
        {
            var book = await _booksRepository.GetByIdAsync(request.Id, cancellationToken);

            if (book is null)
            {
                _logger.LogWarning(
                    "Rejected updating book {BookId}: no such book.",
                    request.Id);

                return Error.NotFound(
                    "Books.NotFound",
                    $"No book with id {request.Id}.");
            }

            var category = await _categorysRepository.GetCategoryByIdAsync(
                request.CategoryId,
                cancellationToken);

            if (category is null)
            {
                _logger.LogWarning(
                    "Rejected updating book {BookId}: no category with id {CategoryId}.",
                    request.Id,
                    request.CategoryId);

                return Error.NotFound(
                    "Books.CategoryNotFound",
                    $"No category with id {request.CategoryId}.");
            }

            var onLoan = await _loansRepository.CountActiveLoansForBookAsync(
                request.Id,
                cancellationToken);

            if (request.TotalCopies < onLoan)
            {
                _logger.LogWarning(
                    "Rejected updating book {BookId}: {TotalCopies} total copies is " +
                    "below the {OnLoan} currently on loan.",
                    request.Id,
                    request.TotalCopies,
                    onLoan);

                return Error.Conflict(
                    "Books.CopiesBelowActiveLoans",
                    $"{onLoan} cop{(onLoan == 1 ? "y is" : "ies are")} currently on " +
                    $"loan, so total copies cannot be set below {onLoan}.");
            }

            book.Title = request.Title;
            book.Author = request.Author;
            book.CategoryId = request.CategoryId;
            book.TotalCopies = request.TotalCopies;

            var updated = await _booksRepository.UpdateAsync(book, cancellationToken);

            _logger.LogInformation(
                "Updated book {BookId} ({Title}); total copies now {TotalCopies}.",
                updated.Id,
                updated.Title,
                updated.TotalCopies);

            return updated.Adapt<BooksDTO>() with { CopiesOnLoan = onLoan };
        }
    }
}
