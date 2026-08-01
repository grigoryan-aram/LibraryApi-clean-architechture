using FluentValidation;

namespace Application.Features.Loans.Queries
{
    public class GetLoanByIdQueryValidator : AbstractValidator<GetLoanByIdQuery>
    {
        public GetLoanByIdQueryValidator()
        {

            RuleFor(x => x.Id)
                .GreaterThan(0).WithMessage("Id must be greater than 0.");

        }
    }
}
