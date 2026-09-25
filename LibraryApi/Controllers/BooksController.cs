using Application.Features.Books.Commands;
using Application.Features.Books.Queries;
using LibraryApi.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;


namespace LibraryApi.Controllers;

[EnableCors]
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class BooksController : ControllerBase
{
    private readonly IMediator _mediator;


    public BooksController(IMediator mediator)
    {
        _mediator = mediator;
    }


    /// <summary>
    /// Paged catalogue. Supports ?page=, ?pageSize= (max 100), ?search=
    /// over title and author, ?sortBy=id|title|author|totalCopies and
    /// ?descending=.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetBooks([FromQuery] SearchBooksQuery query)
    {
        var result = await _mediator.Send(query);

        return result.Match(
            books => Ok(books),
            errors => this.ToProblem(errors));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetBookById([FromRoute] int id)
    {
        var result = await _mediator.Send(new GetBookByIdQuery(id));

        return result.Match(
               book => Ok(book),
               errors => this.ToProblem(errors));
    }


    [HttpPost]
    public async Task<IActionResult> AddBook([FromBody] AddBookCommand command)
    {

        var result = await _mediator.Send(command);

        return result.Match(
               book => Ok(book),
               errors => this.ToProblem(errors));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateBook([FromRoute] int id, [FromBody] UpdateBookCommand command)
    {
        var result = await _mediator.Send(command with { Id = id });

        return result.Match(
               book => Ok(book),
               errors => this.ToProblem(errors));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteBook([FromRoute] int id)
    {
        var result = await _mediator.Send(new DeleteBookCommand(id));

        return result.Match(
        _ => NoContent(),
        errors => this.ToProblem(errors));
    }




    




}
