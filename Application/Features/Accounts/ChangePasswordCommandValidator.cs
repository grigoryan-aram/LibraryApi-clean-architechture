using FluentValidation;

namespace Application.Features.Accounts
{
    // Presence and "actually different" only. Strength stays with Identity,
    // the same as registration and reset, so there is one set of rules.
    public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
    {
        public ChangePasswordCommandValidator()
        {
            RuleFor(x => x.IdentityUserId)
                .NotEmpty()
                .WithMessage("You must be signed in to change your password.");

            RuleFor(x => x.CurrentPassword)
                .NotEmpty()
                .WithMessage("Enter your current password.");

            RuleFor(x => x.NewPassword)
                .NotEmpty()
                .WithMessage("Enter a new password.")
                .NotEqual(x => x.CurrentPassword)
                .WithMessage("The new password must be different from the current one.");
        }
    }
}
