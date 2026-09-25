using Application.Features.Books.Queries;
using LibraryApi.Domain.Entities;
using LibraryApi.Domain.RepositoryInterfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Application.UnitTests.Features.Books;

public class SearchBooksQueryHandlerTests
{
    private readonly Mock<IBooksRepository> _books = new();
    private readonly Mock<ILoansRepository> _loans = new();

    private SearchBooksQueryHandler CreateSut() =>
        new(_books.Object, _loans.Object, NullLogger<SearchBooksQueryHandler>.Instance);

    private static BookModel Book(int id) => new()
    {
        Id = id,
        Title = $"Book {id}",
        Author = "Author",
        CategoryId = 1,
        TotalCopies = 3
    };

    private void GivenThePageIs(int totalCount, params int[] ids) =>
        _books.Setup(repo => repo.SearchAsync(
                  It.IsAny<string?>(),
                  It.IsAny<string?>(),
                  It.IsAny<bool>(),
                  It.IsAny<int>(),
                  It.IsAny<int>(),
                  It.IsAny<CancellationToken>()))
              .ReturnsAsync((
                  (IReadOnlyList<BookModel>)ids.Select(Book).ToList(),
                  totalCount));

    private void GivenNothingIsOnLoan() =>
        _loans.Setup(repo => repo.CountActiveLoansByBookAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(new Dictionary<int, int>());

    // Page is 1-based on the wire and skip is 0-based in SQL. Getting this off
    // by one returns the wrong page silently rather than failing.
    [Theory]
    [InlineData(1, 20, 0)]
    [InlineData(2, 20, 20)]
    [InlineData(5, 10, 40)]
    public async Task Translates_the_page_number_into_the_right_skip(
        int page, int pageSize, int expectedSkip)
    {
        GivenThePageIs(0);
        GivenNothingIsOnLoan();

        await CreateSut().Handle(
            new SearchBooksQuery(Page: page, PageSize: pageSize),
            CancellationToken.None);

        _books.Verify(repo => repo.SearchAsync(
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<bool>(),
            expectedSkip,
            pageSize,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reports_the_total_count_rather_than_the_page_length()
    {
        GivenThePageIs(57, 1, 2, 3);
        GivenNothingIsOnLoan();

        var result = await CreateSut().Handle(
            new SearchBooksQuery(Page: 1, PageSize: 3),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(3, result.Value.Items.Count);
        Assert.Equal(57, result.Value.TotalCount);
        Assert.Equal(19, result.Value.TotalPages);
    }

    [Theory]
    [InlineData(1, 20, 57, false, true)]
    [InlineData(2, 20, 57, true, true)]
    [InlineData(3, 20, 57, true, false)]
    public async Task Reports_whether_there_is_a_page_either_side(
        int page, int pageSize, int totalCount, bool hasPrevious, bool hasNext)
    {
        GivenThePageIs(totalCount);
        GivenNothingIsOnLoan();

        var result = await CreateSut().Handle(
            new SearchBooksQuery(Page: page, PageSize: pageSize),
            CancellationToken.None);

        Assert.Equal(hasPrevious, result.Value.HasPreviousPage);
        Assert.Equal(hasNext, result.Value.HasNextPage);
    }

    [Fact]
    public async Task Returns_an_empty_page_rather_than_an_error_when_nothing_matches()
    {
        GivenThePageIs(0);
        GivenNothingIsOnLoan();

        var result = await CreateSut().Handle(
            new SearchBooksQuery(Search: "nothing matches this"),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Empty(result.Value.Items);
        Assert.Equal(0, result.Value.TotalCount);
        Assert.False(result.Value.HasNextPage);
    }

    // CopiesOnLoan has no counterpart on BookModel, so Mapster leaves it 0 and
    // the handler has to supply it. Forget it and every page claims the whole
    // catalogue is free.
    [Fact]
    public async Task Fills_in_the_copies_on_loan_for_each_book_on_the_page()
    {
        GivenThePageIs(2, 1, 2);
        _loans.Setup(repo => repo.CountActiveLoansByBookAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(new Dictionary<int, int> { [1] = 2 });

        var result = await CreateSut().Handle(
            new SearchBooksQuery(),
            CancellationToken.None);

        Assert.Equal(2, result.Value.Items[0].CopiesOnLoan);
        Assert.Equal(1, result.Value.Items[0].AvailableCopies);

        // Absent from the dictionary means none out, not unknown.
        Assert.Equal(0, result.Value.Items[1].CopiesOnLoan);
    }

    [Fact]
    public async Task Passes_the_search_and_sort_through_to_the_repository()
    {
        GivenThePageIs(0);
        GivenNothingIsOnLoan();

        await CreateSut().Handle(
            new SearchBooksQuery(Search: "dune", SortBy: "title", Descending: true),
            CancellationToken.None);

        _books.Verify(repo => repo.SearchAsync(
            "dune",
            "title",
            true,
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
