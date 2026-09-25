using Application.DTOs;
using ErrorOr;
using LibraryApi.Domain.RepositoryInterfaces;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Books.Queries
{
    public class SearchBooksQueryHandler
        : IRequestHandler<SearchBooksQuery, ErrorOr<PagedResult<BooksDTO>>>
    {
        private readonly IBooksRepository _booksRepository;
        private readonly ILoansRepository _loansRepository;
        private readonly ILogger<SearchBooksQueryHandler> _logger;

        public SearchBooksQueryHandler(
            IBooksRepository booksRepository,
            ILoansRepository loansRepository,
            ILogger<SearchBooksQueryHandler> logger)
        {
            _booksRepository = booksRepository;
            _loansRepository = loansRepository;
            _logger = logger;
        }

        public async Task<ErrorOr<PagedResult<BooksDTO>>> Handle(
            SearchBooksQuery request,
            CancellationToken cancellationToken)
        {
            var (books, totalCount) = await _booksRepository.SearchAsync(
                request.Search,
                request.SortBy,
                request.Descending,
                skip: (request.Page - 1) * request.PageSize,
                take: request.PageSize,
                cancellationToken);

            // Still the one GROUP BY that GetAllBooksQuery uses rather than a
            // count per book, so paging has not reintroduced the N+1 that
            // method exists to avoid. It counts across the whole table, not
            // just this page — cheap, and it keeps the two list paths
            // answering identically.
            var onLoan = await _loansRepository.CountActiveLoansByBookAsync(
                cancellationToken);

            var items = books
                .Select(book => book.Adapt<BooksDTO>() with
                {
                    CopiesOnLoan = onLoan.TryGetValue(book.Id, out var count) ? count : 0
                })
                .ToList();

            _logger.LogInformation(
                "Returned page {Page} of books ({Returned} of {TotalCount}) " +
                "for search {Search}.",
                request.Page,
                items.Count,
                totalCount,
                request.Search ?? "(none)");

            return new PagedResult<BooksDTO>(
                items,
                request.Page,
                request.PageSize,
                totalCount);
        }
    }
}
