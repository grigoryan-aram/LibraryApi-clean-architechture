using ErrorOr;
using LibraryApi.Application.DTOs;
using LibraryApi.Application.RepositoryInterfaces;
using Mapster;
using MediatR;

namespace Application.Features.Books.Queries
{
    public class GetBookByIdQueryHandler : IRequestHandler<GetBookByIdQuery, ErrorOr<BooksDTO>>
    {

        private readonly IBooksRepository _booksRepository;

        public GetBookByIdQueryHandler(IBooksRepository booksRepository)
        {
            _booksRepository = booksRepository;
        }


        public async Task<ErrorOr<BooksDTO>> Handle(GetBookByIdQuery request, CancellationToken cancellationToken)
        {

            var book = await _booksRepository.GetByIdAsync(request.id, cancellationToken);

            if (book == null)
            {
                return Error.NotFound("Book.NotFound", $"Book with id {request.id} not found.");
            }

            return book.Adapt<BooksDTO>();
        }
    }
}
