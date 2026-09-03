using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Application.DTOs;
using Application.ServiceInterfaces;
using ErrorOr;
using Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services
{
    public class ClaudeService : IClaudeService
    {
        private readonly ClaudeSettings _settings;

        private readonly ILogger<ClaudeService> _logger;

        // Built on first use rather than in the constructor: with no key
        // configured the service still resolves and AskAsync returns a proper
        // ErrorOr instead of the container throwing on every request.
        private readonly Lazy<AnthropicClient> _client;

        public ClaudeService(IOptions<ClaudeSettings> settings, ILogger<ClaudeService> logger)
        {
            _settings = settings.Value;
            _logger = logger;

            _client = new Lazy<AnthropicClient>(() => new AnthropicClient
            {
                ApiKey = _settings.ApiKey
            });
        }

        public async Task<ErrorOr<ClaudeReplyDTO>> AskAsync(
            IReadOnlyList<ChatMessageDTO> conversation,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                _logger.LogError(
                    "No Claude API key is configured. Set Claude:ApiKey in user " +
                    "secrets or the ANTHROPIC_API_KEY environment variable.");

                return Error.Failure(
                    "Claude.ApiKeyMissing",
                    "No Claude API key is configured. Set Claude:ApiKey in user " +
                    "secrets or the ANTHROPIC_API_KEY environment variable.");
            }

            var parameters = new MessageCreateParams
            {
                Model = _settings.Model,
                MaxTokens = _settings.MaxTokens,
                System = _settings.SystemPrompt,
                Messages = conversation.Select(ToMessageParam).ToList(),

                // Thinking is adaptive by default on Claude Opus 5, so it is
                // left unset. Effort is what trades depth against latency, and
                // this call is synchronous from Swagger's point of view.
                OutputConfig = new OutputConfig { Effort = Effort.Medium }
            };

            try
            {
                var response = await _client.Value.Messages.Create(
                    parameters,
                    cancellationToken);

                if (response.StopReason == "refusal")
                {
                    return Error.Failure(
                        "Claude.Refused",
                        "Claude declined to answer that message.");
                }

                var reply = string.Join(
                    "\n\n",
                    response.Content
                        .Select(block => block.Value)
                        .OfType<TextBlock>()
                        .Select(text => text.Text));

                if (string.IsNullOrWhiteSpace(reply))
                {
                    return Error.Failure(
                        "Claude.EmptyReply",
                        "Claude returned no text content.");
                }

                return new ClaudeReplyDTO(
                    reply,
                    _settings.Model,
                    response.Usage.InputTokens,
                    response.Usage.OutputTokens);
            }
            catch (AnthropicUnauthorizedException)
            {
                return Error.Failure(
                    "Claude.Unauthorized",
                    "The configured Claude API key was rejected.");
            }
            catch (AnthropicRateLimitException)
            {
                return Error.Failure(
                    "Claude.RateLimited",
                    "Claude is rate limiting this key. Try again shortly.");
            }
            catch (Anthropic5xxException)
            {
                return Error.Failure(
                    "Claude.Unavailable",
                    "Claude is temporarily unavailable. Try again shortly.");
            }
            catch (AnthropicIOException)
            {
                return Error.Failure(
                    "Claude.Unreachable",
                    "Could not reach the Claude API.");
            }
            catch (AnthropicApiException exception)
            {
                return Error.Failure(
                    "Claude.ApiError",
                    $"Claude rejected the request: {exception.Message}");
            }
        }

        private static MessageParam ToMessageParam(ChatMessageDTO message) =>
            new()
            {
                Role = message.Role == ChatRoles.Assistant
                    ? Role.Assistant
                    : Role.User,
                Content = message.Content
            };
    }
}
