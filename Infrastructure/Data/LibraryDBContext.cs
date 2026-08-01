using LibraryApi.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LibraryApi.Infrastructure.Data;

public class LibraryDBContext : IdentityDbContext<IdentityUser>
{
    public LibraryDBContext(DbContextOptions<LibraryDBContext> options)
       : base(options)
    { }

    public DbSet<BookModel> Books { get; set; }
    public DbSet<MemberModel> Members { get; set; }
    public DbSet<CategoryModel> Categories { get; set; }
    public DbSet<LoanModel> Loans { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BookModel>()
            .HasOne(b => b.Category)
            .WithMany(c => c.Books)
            .HasForeignKey(b => b.CategoryId);


        modelBuilder.Entity<LoanModel>()
            .HasOne(l => l.Book)
            .WithMany(b => b.Loans)
            .HasForeignKey(l => l.BookId);


        modelBuilder.Entity<LoanModel>()
            .HasOne(l => l.Member)
            .WithMany(m => m.Loans)
            .HasForeignKey(l => l.MemberId);


        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LibraryDBContext).Assembly);

        base.OnModelCreating(modelBuilder);




    }

}






