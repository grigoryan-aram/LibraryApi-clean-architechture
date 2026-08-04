using Application.RepositoryInterfaces;
using ErrorOr;
using MediatR;


namespace Application.Features.Categorys.Commands
{
    public class DeleteCategoryCommandHandler : IRequestHandler<DeleteCategoryCommand, ErrorOr<Deleted>>
    {
        private readonly ICategorysRepository _categorysRepository;

        public DeleteCategoryCommandHandler(ICategorysRepository categorysRepository)
        {
            _categorysRepository = categorysRepository;
        }



        public async Task<ErrorOr<Deleted>> Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
        {
            await _categorysRepository.DeleteCategoryAsync(request.Id, cancellationToken);

            return Result.Deleted;
        }
    }
}
