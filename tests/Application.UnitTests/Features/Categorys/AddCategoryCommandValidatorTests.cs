using Application.Features.Categorys.Commands;

namespace Application.UnitTests.Features.Categorys;

public class AddCategoryCommandValidatorTests
{
    private readonly AddCategoryCommandValidator _validator = new();

    private static AddCategoryCommand Valid => new(Name: "Science Fiction");

    [Fact]
    public void Accepts_a_complete_command()
    {
        Assert.True(_validator.Validate(Valid).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_blank_name(string name)
    {
        var result = _validator.Validate(Valid with { Name = name });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AddCategoryCommand.Name));
    }

    [Fact]
    public void Accepts_a_name_of_exactly_a_hundred_characters()
    {
        Assert.True(_validator.Validate(Valid with { Name = new string('x', 100) }).IsValid);
    }

    // 101 is the first value the rule is supposed to start rejecting; asserting
    // that "some very long name" fails would pass against a looser rule too.
    [Fact]
    public void Rejects_a_name_of_a_hundred_and_one_characters()
    {
        Assert.False(_validator.Validate(Valid with { Name = new string('x', 101) }).IsValid);
    }
}
