using Application.Features.Books.Commands;

namespace Application.UnitTests.Features.Books;

public class AddBookCommandValidatorTests
{
    private readonly AddBookCommandValidator _validator = new();

    private static AddBookCommand Valid => new(
        Title: "Dune", Author: "Frank Herbert", CategoryId: 2, TotalCopies: 1);

    [Fact]
    public void Accepts_a_complete_command()
    {
        Assert.True(_validator.Validate(Valid).IsValid);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void Accepts_a_copy_count_inside_one_to_a_hundred(int copies)
    {
        var result = _validator.Validate(Valid with { TotalCopies = copies });

        Assert.True(result.IsValid);
    }

    // The upper bound is the point of this test: it was lowered from 1000 to 100, and
    // 101 is the first value the change is supposed to start rejecting. Asserting only
    // "some large number is rejected" would have passed against the old rule too.
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(101)]
    [InlineData(1000)]
    public void Rejects_a_copy_count_outside_one_to_a_hundred(int copies)
    {
        var result = _validator.Validate(Valid with { TotalCopies = copies });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AddBookCommand.TotalCopies));
    }

    [Fact]
    public void Rejects_an_empty_title()
    {
        var result = _validator.Validate(Valid with { Title = "  " });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AddBookCommand.Title));
    }

    [Fact]
    public void Rejects_a_title_over_fifty_characters()
    {
        var result = _validator.Validate(Valid with { Title = new string('x', 51) });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AddBookCommand.Title));
    }

    [Fact]
    public void Rejects_an_empty_author()
    {
        var result = _validator.Validate(Valid with { Author = "" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AddBookCommand.Author));
    }

    [Fact]
    public void Rejects_an_author_over_fifty_characters()
    {
        var result = _validator.Validate(Valid with { Author = new string('x', 51) });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AddBookCommand.Author));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_a_category_id_that_is_not_positive(int categoryId)
    {
        var result = _validator.Validate(Valid with { CategoryId = categoryId });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AddBookCommand.CategoryId));
    }
}
