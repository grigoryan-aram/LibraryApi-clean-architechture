namespace LibraryApi.Application.DTOs
{

    public class BooksDTO
    {


        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public bool IsBorrowed { get; set; }
        public int CategoryId { get; set; }
    }
}


