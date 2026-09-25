using Application.DTOs;
using ErrorOr;
using MediatR;

namespace Application.Features.Books.Queries
{
    // The paged, filtered catalogue behind GET /api/Books.
    //
    // GetAllBooksQuery still exists and is still unpaged, because the Blazor
    // pages use it in-process to fill dropdowns and genuinely do want every
    // book. Anything crossing HTTP should come through here instead — an
    // endpoint that returns the whole table only looks fine until the table
    // stops being small.
    public record SearchBooksQuery(
        int Page = 1,
        int PageSize = 20,
        string? Search = null,
        string? SortBy = null,
        bool Descending = false)
        : IRequest<ErrorOr<PagedResult<BooksDTO>>>;
}
