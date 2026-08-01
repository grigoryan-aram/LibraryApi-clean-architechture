using ErrorOr;
using LibraryApi.Application.RepositoryInterfaces;
using MediatR;

namespace Application.Features.Books.Commands
{
    public class DeleteBookCommandHandler : IRequestHandler<DeleteBookCommand, ErrorOr<Deleted>>
    {
        private readonly IBooksRepository _booksRepository;

        public DeleteBookCommandHandler(IBooksRepository booksRepository)
        {
            _booksRepository = booksRepository;
        }

        public async Task<ErrorOr<Deleted>> Handle(DeleteBookCommand request, CancellationToken cancellationToken)
        {

            await _booksRepository.DeleteAsync(request.Id, cancellationToken);

            return Result.Deleted;


        }
    }
}
