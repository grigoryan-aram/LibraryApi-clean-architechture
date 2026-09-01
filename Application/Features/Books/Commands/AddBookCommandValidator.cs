using FluentValidation;

namespace Application.Features.Books.Commands
{
    public class AddBookCommandValidator
        : AbstractValidator<AddBookCommand>
    {
        public AddBookCommandValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty()
                .WithMessage("Title is required.")
                .MaximumLength(50)
                .WithMessage("Title cannot exceed 50 characters.");

            RuleFor(x => x.CategoryId)
                .GreaterThan(0)
                .WithMessage("Category ID must be greater than 0.");

            RuleFor(x => x.Author)
                .NotEmpty()
                .WithMessage("Author is required.")
                .MaximumLength(50)
                .WithMessage("Author cannot exceed 50 characters.");

           
        }
    }
}
