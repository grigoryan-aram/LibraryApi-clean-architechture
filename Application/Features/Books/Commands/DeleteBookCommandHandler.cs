using Application.RepositoryInterfaces;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Books.Commands
{
    public class DeleteBookCommandHandler : IRequestHandler<DeleteBookCommand, ErrorOr<Deleted>>
    {
        private readonly IBooksRepository _booksRepository;
        private readonly ILogger<DeleteBookCommandHandler> _logger;

        public DeleteBookCommandHandler(
            IBooksRepository booksRepository,
            ILogger<DeleteBookCommandHandler> logger)
        {
            _booksRepository = booksRepository;
            _logger = logger;
        }

        public async Task<ErrorOr<Deleted>> Handle(DeleteBookCommand request, CancellationToken cancellationToken)
        {

            await _booksRepository.DeleteAsync(request.Id, cancellationToken);

            _logger.LogInformation("Deleted book {BookId}.", request.Id);

            return Result.Deleted;


        }
    }
}
