using FluentValidation;

namespace Application.Features.Login.Commands
{
    public class LoginCommandValidator : AbstractValidator<LoginCommand>
    {
        public LoginCommandValidator()
        {
            RuleFor(x => x.Username)
                .NotEmpty()
                .WithMessage("Username is required.");

            // Presence only. A minimum length used to be checked here, which
            // is the wrong place for it twice over: an account whose password
            // predates a policy change could never sign in again, and a short
            // password came back as "must be at least 6 characters" instead of
            // the same "invalid username or password" every other bad
            // credential gets. Strength belongs to registration and reset.
            RuleFor(x => x.Password)
                .NotEmpty()
                .WithMessage("Password is required.");
        }
    }
}
