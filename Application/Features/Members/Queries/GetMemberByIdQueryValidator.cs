using FluentValidation;
namespace Application.Features.Members.Queries
{
    public class GetMemberByIdQueryValidator : AbstractValidator<GetMemberByIdQuery>
    {
        public GetMemberByIdQueryValidator()
        {
            RuleFor(x => x.Id)
                .NotEmpty().WithMessage("Id is required.")
                .GreaterThan(0).WithMessage("Id must be greater than 0.");
        }
    }
}
