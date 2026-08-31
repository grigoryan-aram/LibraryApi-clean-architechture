using FluentValidation;

namespace Application.Features.Books.Commands
{
    public class UpdateBookCommandValidator
        : AbstractValidator<UpdateBookCommand>
    {
        public UpdateBookCommandValidator()
        {
            RuleFor(x => x.Id)
                .GreaterThan(0)
                .WithMessage("Book Id must be greater than 0.");

            RuleFor(x => x.Title)
                .NotEmpty()
                .WithMessage("Title is required.")
                .MaximumLength(50)
                .WithMessage("Title cannot exceed 50 characters.");

            RuleFor(x => x.Author)
                .NotEmpty()
                .WithMessage("Author is required.")
                .MaximumLength(50)
                .WithMessage("Author cannot exceed 50 characters.");

            RuleFor(x => x.CategoryId)
                .GreaterThan(0)
                .WithMessage("Category ID must be greater than 0.");

            RuleFor(x => x.TotalCopies)
                .GreaterThan(0)
                .WithMessage("A book needs at least one copy.")
                .LessThanOrEqualTo(1000)
                .WithMessage("Total copies cannot exceed 1000.");
        }
    }
}
