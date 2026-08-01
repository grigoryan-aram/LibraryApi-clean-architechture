using FluentValidation;


namespace Application.Features.Members.Commands
{
    public class AddMemberCommandValidator : AbstractValidator<AddMemberCommand>
    {
        public AddMemberCommandValidator()
        {

            RuleFor(x => x.name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(50).WithMessage("Name cannot exceed 50 characters.");

            RuleFor(x => x.id)
                .GreaterThan(0).WithMessage("Id must be greater than 0.");

        }
    }
}
