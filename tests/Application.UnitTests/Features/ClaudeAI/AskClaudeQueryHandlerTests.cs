using Application.DTOs;
using Application.Features.ClaudeAI.Queries;
using Application.ServiceInterfaces;
using ErrorOr;
using Moq;

namespace Application.UnitTests.Features.ClaudeAI;

public class AskClaudeQueryHandlerTests
{
    private const string Requester = "ada";

    private readonly Mock<IClaudeService> _claudeService = new();
    private readonly Mock<IChatHistoryStore> _historyStore = new();
    private readonly Mock<IAiUsageLimiter> _usageLimiter = new();

    private static readonly ClaudeReplyDTO Reply =
        new("Four.", "claude-opus-5", 12, 3);

    public AskClaudeQueryHandlerTests()
    {
        _usageLimiter
            .Setup(l => l.Check(It.IsAny<string>()))
            .Returns(new AiUsageDecision(true, TimeSpan.Zero));

        // IChatHistoryStore.Get returns an empty list for an unknown id, never
        // null. Without this the mock hands back Moq's default null and the
        // failure looks like a handler bug rather than a missing setup.
        _historyStore
            .Setup(s => s.Get(It.IsAny<Guid>()))
            .Returns(Array.Empty<ChatMessageDTO>());
    }

    private AskClaudeQueryHandler CreateSut() =>
        new(_claudeService.Object, _historyStore.Object, _usageLimiter.Object);

    private void GivenClaudeReturns(ErrorOr<ClaudeReplyDTO> reply) =>
        _claudeService
            .Setup(s => s.AskAsync(
                It.IsAny<IReadOnlyList<ChatMessageDTO>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(reply);

    private void GivenStoredHistory(
        Guid conversationId,
        params ChatMessageDTO[] history) =>
        _historyStore
            .Setup(s => s.Get(conversationId))
            .Returns(history);

    private IReadOnlyList<ChatMessageDTO> CapturedConversation()
    {
        IReadOnlyList<ChatMessageDTO> captured = Array.Empty<ChatMessageDTO>();

        _claudeService.Verify(s => s.AskAsync(
            It.Is<IReadOnlyList<ChatMessageDTO>>(c => Capture(c, out captured)),
            It.IsAny<CancellationToken>()));

        return captured;
    }

    private static bool Capture(
        IReadOnlyList<ChatMessageDTO> conversation,
        out IReadOnlyList<ChatMessageDTO> captured)
    {
        captured = conversation;
        return true;
    }

    [Fact]
    public async Task Starts_a_new_conversation_when_no_id_is_supplied()
    {
        GivenClaudeReturns(Reply);

        var result = await CreateSut().Handle(
            new AskClaudeQuery("What is 2 + 2?", null, Requester),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.NotEqual(Guid.Empty, result.Value.ConversationId);
        Assert.Equal("Four.", result.Value.Reply);
        Assert.Equal("claude-opus-5", result.Value.Model);
        Assert.Equal(12, result.Value.InputTokens);
        Assert.Equal(3, result.Value.OutputTokens);
    }

    [Fact]
    public async Task Keeps_the_conversation_id_the_caller_supplied()
    {
        var conversationId = Guid.NewGuid();
        GivenStoredHistory(conversationId);
        GivenClaudeReturns(Reply);

        var result = await CreateSut().Handle(
            new AskClaudeQuery("And again?", conversationId, Requester),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(conversationId, result.Value.ConversationId);
    }

    // The whole point of the store: an ongoing chat has to reach Claude as
    // prior turns plus the new question, in that order.
    [Fact]
    public async Task Sends_the_stored_history_followed_by_the_new_message()
    {
        var conversationId = Guid.NewGuid();
        GivenStoredHistory(
            conversationId,
            new ChatMessageDTO(ChatRoles.User, "What is 2 + 2?"),
            new ChatMessageDTO(ChatRoles.Assistant, "Four."));
        GivenClaudeReturns(Reply);

        await CreateSut().Handle(
            new AskClaudeQuery("Times three?", conversationId, Requester),
            CancellationToken.None);

        var sent = CapturedConversation();

        Assert.Equal(3, sent.Count);
        Assert.Equal(ChatRoles.User, sent[0].Role);
        Assert.Equal("What is 2 + 2?", sent[0].Content);
        Assert.Equal(ChatRoles.Assistant, sent[1].Role);
        Assert.Equal(ChatRoles.User, sent[2].Role);
        Assert.Equal("Times three?", sent[2].Content);
    }

    [Fact]
    public async Task Saves_the_question_and_the_answer_as_the_new_history()
    {
        GivenClaudeReturns(Reply);

        var result = await CreateSut().Handle(
            new AskClaudeQuery("What is 2 + 2?", null, Requester),
            CancellationToken.None);

        _historyStore.Verify(s => s.Save(
            result.Value.ConversationId,
            It.Is<IReadOnlyList<ChatMessageDTO>>(history =>
                history.Count == 2
                && history[0].Role == ChatRoles.User
                && history[0].Content == "What is 2 + 2?"
                && history[1].Role == ChatRoles.Assistant
                && history[1].Content == "Four.")), Times.Once);

        Assert.Equal(2, result.Value.History.Count);
    }

    [Fact]
    public async Task Returns_the_error_and_stores_nothing_when_claude_fails()
    {
        GivenClaudeReturns(Error.Failure(
            "Claude.ApiKeyMissing",
            "No Claude API key is configured."));

        var result = await CreateSut().Handle(
            new AskClaudeQuery("What is 2 + 2?", null, Requester),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Failure, result.FirstError.Type);
        Assert.Equal("Claude.ApiKeyMissing", result.FirstError.Code);

        _historyStore.Verify(s => s.Save(
            It.IsAny<Guid>(),
            It.IsAny<IReadOnlyList<ChatMessageDTO>>()), Times.Never);
    }

    [Fact]
    public async Task Refuses_the_message_when_the_daily_allowance_is_spent()
    {
        _usageLimiter
            .Setup(l => l.Check(Requester))
            .Returns(new AiUsageDecision(false, TimeSpan.FromHours(5)));

        var result = await CreateSut().Handle(
            new AskClaudeQuery("one too many", null, Requester),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Failure, result.FirstError.Type);
        Assert.Equal("Claude.DailyLimitReached", result.FirstError.Code);
        Assert.Contains("5 hours", result.FirstError.Description);

        // The point of the limit: no call to Claude at all.
        _claudeService.Verify(s => s.AskAsync(
            It.IsAny<IReadOnlyList<ChatMessageDTO>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Spends_the_allowance_only_once_claude_has_answered()
    {
        GivenClaudeReturns(Reply);

        await CreateSut().Handle(
            new AskClaudeQuery("a question", null, Requester),
            CancellationToken.None);

        _usageLimiter.Verify(l => l.Record(Requester), Times.Once);
    }

    // A turn that never produced an answer must not cost the caller their one
    // message for the day — otherwise a missing API key locks them out for 24
    // hours over a failure that was never their fault.
    [Fact]
    public async Task Does_not_spend_the_allowance_when_claude_fails()
    {
        GivenClaudeReturns(Error.Failure(
            "Claude.ApiKeyMissing",
            "No Claude API key is configured."));

        await CreateSut().Handle(
            new AskClaudeQuery("a question", null, Requester),
            CancellationToken.None);

        _usageLimiter.Verify(l => l.Record(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Counts_the_allowance_against_the_requester_not_the_conversation()
    {
        GivenClaudeReturns(Reply);

        await CreateSut().Handle(
            new AskClaudeQuery("a question", null, "grace"),
            CancellationToken.None);

        _usageLimiter.Verify(l => l.Check("grace"), Times.Once);
        _usageLimiter.Verify(l => l.Record("grace"), Times.Once);
    }

    // Every stored turn is resent on the next call, so the cap is what stops a
    // long chat from growing the bill without limit.
    [Fact]
    public async Task Drops_the_oldest_turns_once_the_history_passes_the_cap()
    {
        var conversationId = Guid.NewGuid();
        var stored = Enumerable.Range(0, 30)
            .Select(i => new ChatMessageDTO(
                i % 2 == 0 ? ChatRoles.User : ChatRoles.Assistant,
                $"message {i}"))
            .ToArray();

        GivenStoredHistory(conversationId, stored);
        GivenClaudeReturns(Reply);

        var result = await CreateSut().Handle(
            new AskClaudeQuery("the newest question", conversationId, Requester),
            CancellationToken.None);

        var history = result.Value.History;

        Assert.Equal(20, history.Count);
        Assert.Equal("message 12", history[0].Content);
        Assert.Equal("the newest question", history[^2].Content);
        Assert.Equal("Four.", history[^1].Content);
    }
}
