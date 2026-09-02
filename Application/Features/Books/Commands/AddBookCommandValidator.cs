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

            // reduced the maximum number of copies to 100 for better manageability
            RuleFor(x => x.TotalCopies)
                .GreaterThan(0)
                .WithMessage("A book needs at least one copy.")
                .LessThanOrEqualTo(100)
                .WithMessage("Total copies cannot exceed 100.");
        }
    }
}
