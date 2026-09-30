using FluentValidation;

namespace Application.Features.PasswordReset
{
    public class ForgotPasswordCommandValidator
        : AbstractValidator<ForgotPasswordCommand>
    {
        public ForgotPasswordCommandValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("That is not a valid email address.")
                .MaximumLength(256).WithMessage("Email cannot exceed 256 characters.");
        }
    }
}
