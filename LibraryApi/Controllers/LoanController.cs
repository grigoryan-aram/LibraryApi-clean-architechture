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
        var result = await _sender.Send(new GetAllLoansQuery());

        return result.Match(
            loans => Ok(loans),
            errors => Problem(title: errors.First().Description));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetLoanById(int id)
    {
        var result = await _sender.Send(new GetLoanByIdQuery(id));

        return result.Match(
            loan => Ok(loan),
            errors => Problem(title: errors.First().Description));
    }

    [HttpPost]
    public async Task<IActionResult> AddLoan(AddLoanCommand command)
    {
        var result = await _sender.Send(command);

        return result.Match(
            loan => Ok(loan),
            errors => Problem(title: errors.First().Description));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteLoan(int id)
    {
        var result = await _sender.Send(new DeleteLoanCommand(id));

        return result.Match<IActionResult>(
            _ => NoContent(),
            errors => Problem(title: errors.First().Description));
    }
}