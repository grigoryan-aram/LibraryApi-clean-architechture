using Application.Features.Books.Commands;

namespace Application.UnitTests.Features.Books;

public class UpdateBookCommandValidatorTests
{
    private readonly UpdateBookCommandValidator _validator = new();

    private static UpdateBookCommand Valid => new(
        Id: 1, Title: "Dune", Author: "Frank Herbert", CategoryId: 2, TotalCopies: 1);

    [Fact]
    public void Accepts_a_complete_command()
    {
        Assert.True(_validator.Validate(Valid).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_an_id_that_is_not_positive(int id)
    {
        var result = _validator.Validate(Valid with { Id = id });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateBookCommand.Id));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void Accepts_a_copy_count_inside_one_to_a_hundred(int copies)
    {
        var result = _validator.Validate(Valid with { TotalCopies = copies });

        Assert.True(result.IsValid);
    }

    // The bound here must match AddBookCommandValidator. When Update allowed 1000 while
    // Add allowed 100, the lower cap was bypassable: create a book at 100 copies, then
    // PUT it to 500. 1000 is kept as an explicit case because it was the old limit and
    // is the value that would silently start passing again if the two drift apart.
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(101)]
    [InlineData(1000)]
    public void Rejects_a_copy_count_outside_one_to_a_hundred(int copies)
    {
        var result = _validator.Validate(Valid with { TotalCopies = copies });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateBookCommand.TotalCopies));
    }

    [Fact]
    public void Rejects_an_empty_title()
    {
        var result = _validator.Validate(Valid with { Title = "  " });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateBookCommand.Title));
    }

    [Fact]
    public void Rejects_an_empty_author()
    {
        var result = _validator.Validate(Valid with { Author = "" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateBookCommand.Author));
    }
}
