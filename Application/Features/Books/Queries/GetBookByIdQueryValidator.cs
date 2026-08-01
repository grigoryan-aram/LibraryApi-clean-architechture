using FluentValidation;

namespace Application.Features.Books.Queries
{
    public class GetBookByIdQueryValidator : AbstractValidator<GetBookByIdQuery>
    {
        public GetBookByIdQueryValidator()
        {
            RuleFor(x => x.id)
                .NotEmpty()
                .GreaterThan(0).WithMessage("Book Id must be greater than 0.");
        }
    }
}
