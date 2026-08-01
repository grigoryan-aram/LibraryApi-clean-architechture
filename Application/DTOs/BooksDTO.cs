namespace LibraryApi.Application.DTOs
{

    public class BooksDTO
    {
        public string Title { get; init; } = string.Empty;
        public string Author { get; init; } = string.Empty;
        public bool IsBorrowed { get; init; }
        public int CategoryId { get; init; }
    }
}


