using FluentValidation;


namespace Application.Features.Categorys.Commands
{
    public class AddCategoryCommandValidator : AbstractValidator<AddCategoryCommand>
    {
        public AddCategoryCommandValidator()
        {
            RuleFor(x => x.title)
                .NotEmpty().WithMessage("Category name is required.")
                .MaximumLength(100).WithMessage("Category name cannot exceed 100 characters.");

            RuleFor(x => x.id)
                .GreaterThan(0).WithMessage("Category ID must be greater than 0.");
        }
    }
}
