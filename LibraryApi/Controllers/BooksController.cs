using Application.Features.Books.Commands;
using Application.Features.Books.Queries;
using LibraryApi.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace LibraryApi.Controllers;


[Authorize]
[ApiController]
[Route("api/[controller]")]
public class BooksController : ControllerBase
{
    // implement ISender interface to send commands and queries to the mediator
    private readonly IMediator _mediator;


    public BooksController(IMediator mediator)
    {
       
        _mediator = mediator;
    }


    [HttpGet]
    public async Task<IActionResult> GetBooks()
    {
        var result = await _mediator.Send(new GetAllBooksQuery());

        return result.Match(
            books => Ok(books),
            errors => this.ToProblem(errors));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetBookById(int id)
    {
        var result = await _mediator.Send(new GetBookByIdQuery(id));

        return result.Match(
               book => Ok(book),
               errors => this.ToProblem(errors));
    }


    [HttpPost]
    public async Task<IActionResult> AddBook(AddBookCommand command)
    {

        var result = await _mediator.Send(command);

        return result.Match(
               book => Ok(book),
               errors => this.ToProblem(errors));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateBook(int id, UpdateBookCommand command)
    {
        var result = await _mediator.Send(command with { Id = id });

        return result.Match(
               book => Ok(book),
               errors => this.ToProblem(errors));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteBook(int id)
    {
        var result = await _mediator.Send(new DeleteBookCommand(id));

        return result.Match(
        _ => NoContent(),
        errors => this.ToProblem(errors));
    }

}
