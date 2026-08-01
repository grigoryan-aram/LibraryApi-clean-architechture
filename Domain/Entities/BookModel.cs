
namespace LibraryApi.Domain.Entities;

public class BookModel
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public bool IsBorrowed { get; set; }

    public int CategoryId { get; set; }
    public CategoryModel? Category { get; set; }


    public ICollection<LoanModel> Loans { get; set; } = new List<LoanModel>();

}