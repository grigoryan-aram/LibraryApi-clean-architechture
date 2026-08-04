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


        public GetAllBooksQueryHandler(IBooksRepository booksRepository)
        {
            _booksRepository = booksRepository;
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

            var booksDto = books.Adapt<IReadOnlyList<BooksDTO>>();

            return ErrorOrFactory.From(booksDto);
        }
    }
}
