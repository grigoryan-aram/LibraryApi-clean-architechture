using FluentValidation;

namespace Application.Features.Categorys.Query
{
    public class GetCategoryByIdQueryValidator : AbstractValidator<GetCategoryByIdQuery>
    {

        public GetCategoryByIdQueryValidator()
        {

            RuleFor(x => x.Id)
           .NotEmpty().WithMessage("Id is required.")
           .GreaterThan(0).WithMessage("Id must be greater then 0.");

        }

    }
}
