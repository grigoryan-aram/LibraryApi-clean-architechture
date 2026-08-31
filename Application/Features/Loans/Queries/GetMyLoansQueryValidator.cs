using FluentValidation;

namespace Application.Features.Loans.Queries
{
    public class GetMyLoansQueryValidator : AbstractValidator<GetMyLoansQuery>
    {
        public GetMyLoansQueryValidator()
        {
            RuleFor(x => x.IdentityUserId)
                .NotEmpty().WithMessage("The caller could not be identified.");
        }
    }
}
