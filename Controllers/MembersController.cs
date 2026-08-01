using Application.Features.Members.Commands;
using Application.Features.Members.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace LibraryApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MembersController : ControllerBase
{
    private readonly ISender _sender;


    public MembersController(ISender sender)
    {
        _sender = sender;
    }
    [HttpGet]
    public async Task<IActionResult> GetMembers()
    {
        var result = await _sender.Send(new GetAllMembersQuery());

        return Ok(result);
    }


    [HttpGet("{id}")]
    public async Task<IActionResult> GetMemberById(int id)
    {
        var result = await _sender.Send(new GetMemberByIdQuery(id));

        return Ok(result);

    }


    [HttpPost]
    public async Task<IActionResult> AddMember(AddMemberCommand command)
    {

        var result = await _sender.Send(command);

        return Ok(result);

    }


    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMember(int id)
    {

        await _sender.Send(new DeleteMemberCommand(id));

        return NoContent();

    }
}

