using FluentValidation;

namespace Application.Features.Loans.Commands
{
    public class AddLoanCommandValidator : AbstractValidator<AddLoanCommand>
    {

        public AddLoanCommandValidator()
        {

            RuleFor
                (x => x.BookId)
                .GreaterThan(0).WithMessage("BookId must be greater than 0.");

            RuleFor
                (x => x.MemberId)
                .GreaterThan(0).WithMessage("MemberId must be greater than 0.");

            // Nothing here validates the dates any more: the handler stamps
            // BorrowedAt and DueAt from the server clock, so there is no
            // client input left to disbelieve. Whether the two ids actually
            // exist is a database question, and lives in the handler.

        }

    }
}
