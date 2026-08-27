using Application.Features.Loans.Commands;

namespace Application.UnitTests.Features.Loans;

public class ReturnLoanCommandValidatorTests
{
    private readonly ReturnLoanCommandValidator _validator = new();

    [Fact]
    public void Accepts_a_real_looking_id()
    {
        Assert.True(_validator.Validate(new ReturnLoanCommand(7)).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_an_id_that_cannot_exist(int id)
    {
        var result = _validator.Validate(new ReturnLoanCommand(id));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            e => e.PropertyName == nameof(ReturnLoanCommand.Id));
    }
}
