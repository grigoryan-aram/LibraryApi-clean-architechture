using FluentValidation;
using LibraryApi.Domain.Constants;

namespace Application.Features.PasswordReset
{
    public class ResetPasswordCommandValidator
        : AbstractValidator<ResetPasswordCommand>
    {
        public ResetPasswordCommandValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("That is not a valid email address.");

            RuleFor(x => x.Code)
                .NotEmpty().WithMessage("The code from the email is required.")
                .Length(PasswordResetRules.CodeLength)
                .WithMessage($"The code is {PasswordResetRules.CodeLength} characters.");

            // Length and strength are Identity's to judge, and it returns real
            // messages for them, so only emptiness is checked here. Two sets of
            // password rules would drift.
            RuleFor(x => x.NewPassword)
                .NotEmpty().WithMessage("A new password is required.");
        }
    }
}
