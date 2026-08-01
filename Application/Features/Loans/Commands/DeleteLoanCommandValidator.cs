using FluentValidation;


namespace Application.Features.Loans.Commands
{
    public class DeleteLoanCommandValidator : AbstractValidator<DeleteLoanCommand>
    {
        public DeleteLoanCommandValidator()
        {
            RuleFor(x => x.Id)
                .GreaterThan(0).WithMessage("Loan ID must be greater than 0.");
        }
    }
}
