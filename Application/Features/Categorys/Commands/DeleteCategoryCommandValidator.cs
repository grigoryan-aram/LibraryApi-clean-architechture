using FluentValidation;

namespace Application.Features.Categorys.Commands
{
    public class DeleteCategoryCommandValidator : AbstractValidator<DeleteCategoryCommand>
    {
        public DeleteCategoryCommandValidator()
        {
            RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Category ID must be greater than 0.");
        }
    }
}
