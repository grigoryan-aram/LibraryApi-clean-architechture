using Application.DTOs;
using Application.RepositoryInterfaces;
using ErrorOr;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Books.Queries
{
    public class GetBookByIdQueryHandler : IRequestHandler<GetBookByIdQuery, ErrorOr<BooksDTO>>
    {

        private readonly IBooksRepository _booksRepository;
        private readonly ILoansRepository _loansRepository;
        private readonly ILogger<GetBookByIdQueryHandler> _logger;

        public GetBookByIdQueryHandler(
            IBooksRepository booksRepository,
            ILoansRepository loansRepository,
            ILogger<GetBookByIdQueryHandler> logger)
        {
            _booksRepository = booksRepository;
            _loansRepository = loansRepository;
            _logger = logger;
        }


        public async Task<ErrorOr<BooksDTO>> Handle(GetBookByIdQuery request, CancellationToken cancellationToken)
        {

            var book = await _booksRepository.GetByIdAsync(request.id, cancellationToken);

            if (book == null)
            {
                _logger.LogWarning("No book with id {BookId}.", request.id);

                return Error.NotFound("Book.NotFound", $"Book with id {request.id} not found.");
            }

            var onLoan = await _loansRepository.CountActiveLoansForBookAsync(
                book.Id,
                cancellationToken);

            _logger.LogInformation(
                "Returned book {BookId} ({Title}); {OnLoan} of {TotalCopies} copies on loan.",
                book.Id,
                book.Title,
                onLoan,
                book.TotalCopies);

            return book.Adapt<BooksDTO>() with { CopiesOnLoan = onLoan };
        }
    }
}
