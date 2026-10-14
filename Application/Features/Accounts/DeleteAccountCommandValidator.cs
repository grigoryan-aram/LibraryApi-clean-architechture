using FluentValidation;

namespace Application.Features.Accounts
{
    public class DeleteAccountCommandValidator : AbstractValidator<DeleteAccountCommand>
    {
        public DeleteAccountCommandValidator()
        {
            RuleFor(x => x.IdentityUserId)
                .NotEmpty()
                .WithMessage("You must be signed in to delete your account.");

            RuleFor(x => x.Password)
                .NotEmpty()
                .WithMessage("Enter your password to confirm.");
        }
    }
}
