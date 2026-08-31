using Application.DTOs;
using Application.RepositoryInterfaces;
using ErrorOr;
using Mapster;
using MediatR;

namespace Application.Features.Books.Queries
{
    public class GetAllBooksQueryHandler : IRequestHandler<GetAllBooksQuery, ErrorOr<IReadOnlyList<BooksDTO>>>
    {

        private readonly IBooksRepository _booksRepository;
        private readonly ILoansRepository _loansRepository;


        public GetAllBooksQueryHandler(
            IBooksRepository booksRepository,
            ILoansRepository loansRepository)
        {
            _booksRepository = booksRepository;
            _loansRepository = loansRepository;
        }


        public async Task<ErrorOr<IReadOnlyList<BooksDTO>>> Handle(
            GetAllBooksQuery request,
            CancellationToken cancellationToken)
        {
            var books = await _booksRepository.GetAllAsync(cancellationToken);

            if (books == null)
            {
                return Error.NotFound("Books not found");
            }

            var onLoan = await _loansRepository.CountActiveLoansByBookAsync(cancellationToken);

            var booksDto = books
                .Select(book => book.Adapt<BooksDTO>() with
                {
                    CopiesOnLoan = onLoan.TryGetValue(book.Id, out var count) ? count : 0
                })
                .ToList();

            return ErrorOrFactory.From<IReadOnlyList<BooksDTO>>(booksDto);
        }
    }
}
