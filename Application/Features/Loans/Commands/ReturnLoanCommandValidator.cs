using FluentValidation;

namespace Application.Features.Loans.Commands
{
    public class ReturnLoanCommandValidator : AbstractValidator<ReturnLoanCommand>
    {
        public ReturnLoanCommandValidator()
        {
            RuleFor(x => x.Id)
                .GreaterThan(0).WithMessage("Loan ID must be greater than 0.");
        }
    }
}
