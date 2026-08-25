using Application.DTOs;
using ErrorOr;
using Infrastructure.Services;
using Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Infrastructure.UnitTests.Services;

public class ClaudeServiceTests
{
    private static readonly IReadOnlyList<ChatMessageDTO> Conversation =
    [
        new(ChatRoles.User, "What is 2 + 2?")
    ];

    private static ClaudeService CreateSut(ClaudeSettings settings) =>
        new(Options.Create(settings));

    // The missing-key path is the one worth pinning: it has to come back as an
    // ErrorOr the pipeline can turn into a 400, not as an exception from the
    // AnthropicClient constructor on every single request. The Lazy<> in
    // ClaudeService is what keeps that true, and nothing else enforces it.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Returns_an_error_instead_of_calling_the_api_when_no_key_is_configured(
        string apiKey)
    {
        var result = await CreateSut(new ClaudeSettings { ApiKey = apiKey })
            .AskAsync(Conversation, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Failure, result.FirstError.Type);
        Assert.Equal("Claude.ApiKeyMissing", result.FirstError.Code);
    }

    [Fact]
    public void Defaults_to_claude_opus_5_when_no_model_is_configured()
    {
        Assert.Equal("claude-opus-5", new ClaudeSettings().Model);
    }
}
