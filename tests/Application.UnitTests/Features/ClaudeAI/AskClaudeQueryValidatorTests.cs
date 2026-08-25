using Application.Features.ClaudeAI.Queries;

namespace Application.UnitTests.Features.ClaudeAI;

public class AskClaudeQueryValidatorTests
{
    private const string Requester = "ada";

    private readonly AskClaudeQueryValidator _validator = new();

    [Fact]
    public void Accepts_a_message_with_no_conversation_id()
    {
        var result = _validator.Validate(
            new AskClaudeQuery("Hello", null, Requester));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_an_empty_message(string message)
    {
        var result = _validator.Validate(
            new AskClaudeQuery(message, null, Requester));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            e => e.PropertyName == nameof(AskClaudeQuery.Message));
    }

    // The length cap is what keeps one request from turning into a large bill.
    [Fact]
    public void Rejects_a_message_longer_than_the_cap()
    {
        var result = _validator.Validate(
            new AskClaudeQuery(new string('a', 4001), null, Requester));

        Assert.False(result.IsValid);
    }

    // Not caller-supplied: an empty requester means an entry point failed to
    // identify the user, which would give everyone one shared daily allowance.
    [Fact]
    public void Rejects_a_missing_requester()
    {
        var result = _validator.Validate(
            new AskClaudeQuery("Hello", null, ""));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            e => e.PropertyName == nameof(AskClaudeQuery.Requester));
    }

    [Fact]
    public void Rejects_an_empty_guid_as_a_conversation_id()
    {
        var result = _validator.Validate(
            new AskClaudeQuery("Hello", Guid.Empty, Requester));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            e => e.PropertyName == nameof(AskClaudeQuery.ConversationId));
    }
}
