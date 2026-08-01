using Application.Features.Loans.Commands;
using Application.Features.Loans.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;



namespace LibraryApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class LoansController : ControllerBase
{


    private readonly ISender _sender;

    public LoansController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetLoans()
    {

        var loans = await _sender.Send(new GetAllLoansQuery());

        return Ok(loans);

    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetLoanById(int id)
    {

        var loan = await _sender.Send(new GetLoanByIdQuery(id));

        return Ok(loan);
    }


    [HttpPost]
    public async Task<IActionResult> AddLoan(AddLoanCommand command)
    {
        var loan = _sender.Send(command);

        return Ok(loan);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteLoan(int id)
    {

        await _sender.Send(new DeleteLoanCommand(id));

        return NoContent();
    }
}