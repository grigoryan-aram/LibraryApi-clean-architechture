using LibraryApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibraryApi.Infrastructure.Configurations
{
    public class MembersConfiguration : IEntityTypeConfiguration<MemberModel>
    {

        private static MemberModel Member(
            int id,
            string name
            )
        {
            return new MemberModel
            {
                Id = id,
                Name = name,

            };
        }




        public void Configure(EntityTypeBuilder<MemberModel> builder)
        {

            Member(1, "Aram");
            Member(2, "Artur");
            Member(3, "Narek");
            Member(4, "Arshak");
            Member(5, "Arno");
            Member(6, "Hovsep");
            Member(7, "Tigran");
            Member(8, "Hayk");
            Member(9, "Poxos");
            Member(10, "Petros");



        }
    }
}
