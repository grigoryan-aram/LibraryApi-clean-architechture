namespace Application.DTOs
{

    // Id is what lets a client act on a book it just listed - delete it, or
    // put it on loan. Without it the list is read-only trivia. Mapster fills
    // it by name from BookModel.Id.
    public record BooksDTO(
        int Id,
        string Title,
        string Author,
        int CategoryId,
        int TotalCopies,
        int CopiesOnLoan)
    {
        public int AvailableCopies => Math.Max(0, TotalCopies - CopiesOnLoan);

        public bool IsAvailable => AvailableCopies > 0;
    }
}
