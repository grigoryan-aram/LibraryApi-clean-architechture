namespace Application.DTOs
{
    /// <summary>
    /// One page of results plus enough context for a caller to ask for the
    /// next one.
    /// </summary>
    /// <remarks>
    /// The three derived members are computed getters rather than constructor
    /// parameters, for the same reason <c>LoansDTO.IsOverdue</c> is: Mapster
    /// fills constructor parameters and leaves get-only members alone, so they
    /// cannot drift out of step with <c>TotalCount</c>.
    /// </remarks>
    public record PagedResult<T>(
        IReadOnlyList<T> Items,
        int Page,
        int PageSize,
        int TotalCount)
    {
        public int TotalPages => PageSize <= 0
            ? 0
            : (int)Math.Ceiling(TotalCount / (double)PageSize);

        public bool HasPreviousPage => Page > 1;

        public bool HasNextPage => Page < TotalPages;
    }
}
