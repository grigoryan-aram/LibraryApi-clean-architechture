using Application.RepositoryInterfaces;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;


namespace Application.Features.Categorys.Commands
{
    public class DeleteCategoryCommandHandler : IRequestHandler<DeleteCategoryCommand, ErrorOr<Deleted>>
    {
        private readonly ICategorysRepository _categorysRepository;
        private readonly ILogger<DeleteCategoryCommandHandler> _logger;

        public DeleteCategoryCommandHandler(
            ICategorysRepository categorysRepository,
            ILogger<DeleteCategoryCommandHandler> logger)
        {
            _categorysRepository = categorysRepository;
            _logger = logger;
        }



        public async Task<ErrorOr<Deleted>> Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
        {
            await _categorysRepository.DeleteCategoryAsync(request.Id, cancellationToken);

            _logger.LogInformation("Deleted category {CategoryId}.", request.Id);

            return Result.Deleted;
        }
    }
}
