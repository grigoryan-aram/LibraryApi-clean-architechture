using ErrorOr;
using LibraryApi.Extensions;

namespace Presentation.UnitTests.Extensions;

public class ErrorExtensionsTests
{
    // Every one of these used to answer 400, which made a missing book
    // indistinguishable from a malformed request for any client branching on
    // the status code.
    [Theory]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.Unauthorized, 401)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.Unexpected, 500)]
    [InlineData(ErrorType.Validation, 400)]
    public void Maps_each_error_type_to_its_status_code(ErrorType type, int expected)
    {
        Assert.Equal(expected, ErrorExtensions.StatusCodeFor(type));
    }

    // Deliberately still 400: Failure is the catch-all and several uses of it
    // in this codebase are mislabelled at the source. Pinned so that changing
    // it is a decision rather than an accident.
    [Fact]
    public void Leaves_the_catch_all_failure_type_on_400()
    {
        Assert.Equal(400, ErrorExtensions.StatusCodeFor(ErrorType.Failure));
    }

    [Fact]
    public void Falls_back_to_400_for_an_error_type_it_does_not_know()
    {
        Assert.Equal(400, ErrorExtensions.StatusCodeFor((ErrorType)9999));
    }
}
