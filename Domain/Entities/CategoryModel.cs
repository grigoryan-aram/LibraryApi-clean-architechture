namespace LibraryApi.Domain.Entities;

public class CategoryModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<BookModel> Books { get; set; } = new List<BookModel>();
}



