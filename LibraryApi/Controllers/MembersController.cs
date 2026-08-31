using Application.Features.Members.Commands;
using Application.Features.Members.Queries;
using LibraryApi.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MembersController : ControllerBase
{
    private readonly IMediator _mediator;

    public MembersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetMembers()
    {
        var result = await _mediator.Send(new GetAllMembersQuery());

        return result.Match(
            members => Ok(members),
            errors => this.ToProblem(errors));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetMemberById(int id)
    {
        var result = await _mediator.Send(new GetMemberByIdQuery(id));

        return result.Match(
            member => Ok(member),
            errors => this.ToProblem(errors)
        );
    }

    [HttpPost]
    public async Task<IActionResult> AddMember(AddMemberCommand command)
    {
        var result = await _mediator.Send(command);

        return result.Match(
            member => Ok(member),
            errors => this.ToProblem(errors));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateMember(int id, UpdateMemberCommand command)
    {
        var result = await _mediator.Send(command with { Id = id });

        return result.Match(
            member => Ok(member),
            errors => this.ToProblem(errors));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMember(int id)
    {
        var result = await _mediator.Send(new DeleteMemberCommand(id));

        return result.Match(
            _ => NoContent(),
            errors => this.ToProblem(errors));
    }
}
