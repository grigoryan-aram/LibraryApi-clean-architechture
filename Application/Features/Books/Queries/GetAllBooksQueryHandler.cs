using Application.DTOs;
using Application.RepositoryInterfaces;
using ErrorOr;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Books.Queries
{
    public class GetAllBooksQueryHandler : IRequestHandler<GetAllBooksQuery, ErrorOr<IReadOnlyList<BooksDTO>>>
    {

        private readonly IBooksRepository _booksRepository;
        private readonly ILoansRepository _loansRepository;
        private readonly ILogger<GetAllBooksQueryHandler> _logger;


        public GetAllBooksQueryHandler(
            IBooksRepository booksRepository,
            ILoansRepository loansRepository,
            ILogger<GetAllBooksQueryHandler> logger)
        {
            _booksRepository = booksRepository;
            _loansRepository = loansRepository;
            _logger = logger;
        }


        public async Task<ErrorOr<IReadOnlyList<BooksDTO>>> Handle(
            GetAllBooksQuery request,
            CancellationToken cancellationToken)
        {
            var books = await _booksRepository.GetAllAsync(cancellationToken);

            if (books == null)
            {
                _logger.LogError("The books repository returned no collection.");

                return Error.NotFound("Books not found");
            }

            var onLoan = await _loansRepository.CountActiveLoansByBookAsync(cancellationToken);

            var booksDto = books
                .Select(book => book.Adapt<BooksDTO>() with
                {
                    CopiesOnLoan = onLoan.TryGetValue(book.Id, out var count) ? count : 0
                })
                .ToList();

            _logger.LogInformation("Returned {BookCount} books.", booksDto.Count);

            return ErrorOrFactory.From<IReadOnlyList<BooksDTO>>(booksDto);
        }
    }
}
