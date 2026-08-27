using Application.Features.Loans.Commands;
using Application.Features.Loans.Queries;
using LibraryApi.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;



namespace LibraryApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class LoansController : ControllerBase
{
    private readonly IMediator _mediator;

    public LoansController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetLoans()
    {
        var result = await _mediator.Send(new GetAllLoansQuery());

        return result.Match(
            loans => Ok(loans),
            errors => this.ToProblem(errors));
    }

    // Before {id}, or "overdue" is swallowed by the id route and comes back as
    // a model-binding failure.
    [HttpGet("overdue")]
    public async Task<IActionResult> GetOverdueLoans()
    {
        var result = await _mediator.Send(new GetOverdueLoansQuery());

        return result.Match(
            loans => Ok(loans),
            errors => this.ToProblem(errors));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetLoanById(int id)
    {
        var result = await _mediator.Send(new GetLoanByIdQuery(id));

        return result.Match(
            loan => Ok(loan),
            errors => this.ToProblem(errors));
    }

    [HttpPost]
    public async Task<IActionResult> AddLoan(AddLoanCommand command)
    {
        var result = await _mediator.Send(command);

        return result.Match(
            loan => Ok(loan),
            errors => this.ToProblem(errors));
    }

    // POST rather than PUT: this is not an arbitrary update of the loan, it is
    // one named action on it, and the server supplies the only value that
    // changes.
    [HttpPost("{id}/return")]
    public async Task<IActionResult> ReturnLoan(int id)
    {
        var result = await _mediator.Send(new ReturnLoanCommand(id));

        return result.Match(
            loan => Ok(loan),
            errors => this.ToProblem(errors));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteLoan(int id)
    {
        var result = await _mediator.Send(new DeleteLoanCommand(id));

        return result.Match(
            _ => NoContent(),
            errors => this.ToProblem(errors));
    }
}