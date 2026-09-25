using FluentValidation;

namespace Application.Features.Books.Queries
{
    public class SearchBooksQueryValidator : AbstractValidator<SearchBooksQuery>
    {
        // The sort keys the repository knows how to apply. Anything else is
        // rejected here rather than silently ignored, so a caller who
        // misspells one finds out instead of quietly getting default order.
        private static readonly string[] SortableFields =
            ["id", "title", "author", "totalcopies"];

        public SearchBooksQueryValidator()
        {
            RuleFor(x => x.Page)
                .GreaterThan(0).WithMessage("Page must be 1 or greater.");

            // Capped so a caller cannot ask for the whole table in one request
            // and undo the point of paging.
            RuleFor(x => x.PageSize)
                .InclusiveBetween(1, 100)
                .WithMessage("PageSize must be between 1 and 100.");

            RuleFor(x => x.Search)
                .MaximumLength(200)
                .WithMessage("Search cannot exceed 200 characters.");

            RuleFor(x => x.SortBy)
                .Must(field => string.IsNullOrWhiteSpace(field)
                    || SortableFields.Contains(field.ToLowerInvariant()))
                .WithMessage(
                    $"SortBy must be one of: {string.Join(", ", SortableFields)}.");
        }
    }
}
