
using LibraryApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace LibraryApi.Infrastructure.Configurations
{
    public class BooksConfiguration : IEntityTypeConfiguration<BookModel>
    {
        private static BookModel Book(
       int id,
       string title,
       string author,
       int categoryId,
       int totalCopies = 1)
        {
            return new BookModel
            {
                Id = id,
                Title = title,
                Author = author,
                CategoryId = categoryId,
                TotalCopies = totalCopies
            };
        }
        public void Configure(EntityTypeBuilder<BookModel> builder)
        {
            builder.ToTable(table => table.HasCheckConstraint(
                "CK_Books_TotalCopies_Positive",
                "[TotalCopies] >= 1"));

            builder.HasData
                (
              Book(1, "Clean Code", "Robert C. Martin", 1, totalCopies: 3),
              Book(2, "The Pragmatic Programmer", "Andrew Hunt", 1, totalCopies: 2),
              Book(3, "Design Patterns", "Erich Gamma", 1),
              Book(4, "The Hobbit", "J.R.R. Tolkien", 2, totalCopies: 2),
              Book(5, "Harry Potter and the Sorcerer's Stone", "J.K. Rowling", 2),
              Book(6, "1984", "George Orwell", 3),
              Book(7, "To Kill a Mockingbird", "Harper Lee", 3),
              Book(8, "The Da Vinci Code", "Dan Brown", 4),
              Book(9, "Gone Girl", "Gillian Flynn", 4),
              Book(10, "Rich Dad Poor Dad", "Robert Kiyosaki", 5),
              Book(11, "Pride and Prejudice", "Jane Austen", 6),
              Book(12, "Sapiens", "Yuval Noah Harari", 7),
              Book(13, "The Innovators", "Walter Isaacson", 8),
              Book(14, "Thinking, Fast and Slow", "Daniel Kahneman", 9),
              Book(15, "Watchmen", "Alan Moore", 10)
                );
        }
    }
}
