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
                .NotEmpty().WithMessage("Date is required.")
                .LessThanOrEqualTo(DateTime.Now).WithMessage("Date cannot be in the future/past.");

            RuleFor
                (x => x.ReturnedAt)
                .NotEmpty().WithMessage("ReturnDate is required.")
                .GreaterThanOrEqualTo(x => x.BorrowedAt).WithMessage("ReturnDate cannot be before BorrowedAt.");



        }

    }
}
