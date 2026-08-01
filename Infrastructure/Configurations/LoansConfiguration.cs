using LibraryApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibraryApi.Infrastructure.Configurations
{
    public class LoansConfiguration : IEntityTypeConfiguration<LoanModel>
    {

        public void Configure(EntityTypeBuilder<LoanModel> builder)
        {



            // yet to be implemented




        }
    }
}
