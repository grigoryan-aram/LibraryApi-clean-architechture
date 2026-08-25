using FluentValidation;

namespace Application.Features.Loans.Commands
{
    public class AddLoanCommandValidator : AbstractValidator<AddLoanCommand>
    {

        public AddLoanCommandValidator()
        {

            RuleFor
                (x => x.BookId)
                .NotEmpty().WithMessage("BookId is required.")
                .GreaterThan(0).WithMessage("BookId must be greater than 0.");

            RuleFor
                (x => x.MemberId)
                .NotEmpty().WithMessage("MemberId is required.")
                .GreaterThan(0).WithMessage("MemberId must be greater than 0.");

            RuleFor
                (x => x.BorrowedAt)
                .NotEmpty().WithMessage("BorrowedAt is required.")
                .LessThanOrEqualTo(_ => DateTime.UtcNow.AddMinutes(1))
                    .WithMessage("BorrowedAt cannot be in the future.");

            // ReturnedAt is deliberately optional: a loan that has just been
            // handed out has not been returned yet. Requiring it here made it
            // impossible to create an open loan at all, which is the only kind
            // worth creating.
            RuleFor
                (x => x.ReturnedAt)
                .GreaterThanOrEqualTo(x => x.BorrowedAt)
                    .When(x => x.ReturnedAt.HasValue)
                    .WithMessage("ReturnedAt cannot be before BorrowedAt.");

        }

    }
}
