using FluentValidation;

namespace Application.Features.ClaudeAI.Queries
{
    public class AskClaudeQueryValidator : AbstractValidator<AskClaudeQuery>
    {
        public AskClaudeQueryValidator()
        {
            RuleFor(x => x.Message)
                .NotEmpty().WithMessage("Message is required.")
                .MaximumLength(4000)
                    .WithMessage("Message must be 4000 characters or fewer.");

            RuleFor(x => x.ConversationId)
                .NotEqual(Guid.Empty)
                    .WithMessage(
                        "ConversationId must be omitted to start a new " +
                        "conversation, or be the id returned by a previous reply.");

            // Not caller-supplied — an empty one means an entry point forgot to
            // identify the user, which would hand everyone a shared allowance.
            RuleFor(x => x.Requester)
                .NotEmpty().WithMessage("Requester could not be determined.");
        }
    }
}
