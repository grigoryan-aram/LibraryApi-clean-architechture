using FluentValidation;

namespace Application.Features.Books.Commands
{
    public class DeleteBookCommandValidator : AbstractValidator<DeleteBookCommand>
    {


        public DeleteBookCommandValidator()
        {

            RuleFor(x => x.Id)
                .GreaterThan(0).WithMessage("Book Id must be greater than 0.");


        }
    }
}
