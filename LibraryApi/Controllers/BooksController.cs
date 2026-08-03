using Application.Features.Books.Commands;
using Application.Features.Books.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace LibraryApi.Controllers;


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


    [HttpGet]
    public async Task<IActionResult> GetBooks()
    {
        var result = await _mediator.Send(new GetAllBooksQuery());

        return result.Match(books => Ok(books),
               errors => Problem(title: errors.First().Description));
    }


    [HttpGet("{id}")]
    public async Task<IActionResult> GetBookById(int id)
    {
        var result = await _mediator.Send(new GetBookByIdQuery(id));

        return result.Match(book => Ok(book),
               errors => Problem(title: errors.First().Description));
    }


    [HttpPost]
    public async Task<IActionResult> AddBook(AddBookCommand command)
    {

        var result = await _mediator.Send(command);

        return result.Match(book => Ok(book),
               errors => Problem(title: errors.First().Description));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteBook(int id)
    {
        var result = await _mediator.Send(new DeleteBookCommand(id));

        return result.Match<IActionResult>(
        _ => NoContent(),
        errors => Problem(title: errors.First().Description));
    }

}