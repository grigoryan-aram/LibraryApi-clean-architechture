using Application.Features.ClaudeAI.Queries;
using LibraryApi.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace LibraryApi.Controllers;


[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ClaudeAIController : ControllerBase
{

    private readonly IMediator _mediator;


    public ClaudeAIController(IMediator mediator)
    {

        _mediator = mediator;
    }


    /// <summary>
    /// Asks Claude a question. Leave conversationId empty to start a new chat,
    /// then pass back the id from the reply to continue the same one. Nothing
    /// here touches the library database.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Chat(
        [FromQuery] string message,
        [FromQuery] Guid? conversationId)
    {
        // Taken from the cookie, never from the query string — this is the key
        // the one-message-per-day allowance is counted against.
        var requester = User.Identity?.Name ?? string.Empty;

        var result = await _mediator.Send(
            new AskClaudeQuery(message, conversationId, requester));

        return result.Match(
            chat => Ok(chat),
            errors => this.ToProblem(errors));
    }

}
