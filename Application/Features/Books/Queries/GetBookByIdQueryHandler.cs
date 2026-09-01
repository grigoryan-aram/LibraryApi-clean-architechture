using Application.DTOs;
using Application.RepositoryInterfaces;
using ErrorOr;
using Mapster;
using MediatR;

namespace Application.Features.Books.Queries
{
    public class GetBookByIdQueryHandler : IRequestHandler<GetBookByIdQuery, ErrorOr<BooksDTO>>
    {

        private readonly IBooksRepository _booksRepository;
        private readonly ILoansRepository _loansRepository;

        public GetBookByIdQueryHandler(
            IBooksRepository booksRepository,
            ILoansRepository loansRepository)
        {
            _booksRepository = booksRepository;
            _loansRepository = loansRepository;
        }


        public async Task<ErrorOr<BooksDTO>> Handle(GetBookByIdQuery request, CancellationToken cancellationToken)
        {

            var book = await _booksRepository.GetByIdAsync(request.id, cancellationToken);

            if (book == null)
            {
                return Error.NotFound("Book.NotFound", $"Book with id {request.id} not found.");
            }

            var onLoan = await _loansRepository.CountActiveLoansForBookAsync(
                book.Id,
                cancellationToken);

            return book.Adapt<BooksDTO>() with { CopiesOnLoan = onLoan };
        }
    }
}
