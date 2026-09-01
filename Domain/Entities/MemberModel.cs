namespace LibraryApi.Domain.Entities;

public class MemberModel
{
    public int Id { get; set; }

    public string Name { get; set; } = String.Empty;

    public string? IdentityUserId { get; set; }

    public ICollection<LoanModel> Loans { get; set; } = new List<LoanModel>();
}
