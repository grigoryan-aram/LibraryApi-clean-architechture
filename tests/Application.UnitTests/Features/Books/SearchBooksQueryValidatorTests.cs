using Application.Features.Books.Queries;

namespace Application.UnitTests.Features.Books;

public class SearchBooksQueryValidatorTests
{
    private readonly SearchBooksQueryValidator _validator = new();

    [Fact]
    public void Accepts_the_defaults()
    {
        Assert.True(_validator.Validate(new SearchBooksQuery()).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_a_page_below_one(int page)
    {
        Assert.False(_validator.Validate(new SearchBooksQuery(Page: page)).IsValid);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void Accepts_a_page_size_inside_one_to_a_hundred(int pageSize)
    {
        Assert.True(_validator.Validate(new SearchBooksQuery(PageSize: pageSize)).IsValid);
    }

    // 101 is the first value the cap is supposed to start rejecting. The cap
    // is the whole point of paging, so asserting that some huge number fails
    // would pass against a much looser rule too.
    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Rejects_a_page_size_outside_one_to_a_hundred(int pageSize)
    {
        Assert.False(_validator.Validate(new SearchBooksQuery(PageSize: pageSize)).IsValid);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("title")]
    [InlineData("author")]
    [InlineData("totalcopies")]
    [InlineData("TITLE")]
    [InlineData(null)]
    [InlineData("")]
    public void Accepts_a_sort_key_the_repository_knows(string? sortBy)
    {
        Assert.True(_validator.Validate(new SearchBooksQuery(SortBy: sortBy)).IsValid);
    }

    // Rejected rather than ignored: a misspelled sort key that silently
    // returns default order is a bug the caller cannot see.
    [Theory]
    [InlineData("titel")]
    [InlineData("publisher")]
    [InlineData("1; DROP TABLE Books")]
    public void Rejects_a_sort_key_it_cannot_apply(string sortBy)
    {
        Assert.False(_validator.Validate(new SearchBooksQuery(SortBy: sortBy)).IsValid);
    }

    [Fact]
    public void Rejects_a_search_term_over_two_hundred_characters()
    {
        Assert.False(_validator
            .Validate(new SearchBooksQuery(Search: new string('x', 201))).IsValid);
    }
}
