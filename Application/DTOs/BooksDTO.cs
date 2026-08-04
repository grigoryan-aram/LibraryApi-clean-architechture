namespace Application.DTOs
{

    public record BooksDTO(
        string Title,
        string Author,
        bool IsBorrowed,
        int CategoryId);
}


