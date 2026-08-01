using LibraryApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace LibraryApi.Infrastructure.Configurations
{
    public class CategorysConfiguration : IEntityTypeConfiguration<CategoryModel>
    {

        private static CategoryModel Category
            (
            int id,
            string Name
            )
        {
            return new CategoryModel
            {
                Id = id,
                Name = Name

            };

        }




        public void Configure(EntityTypeBuilder<CategoryModel> builder)
        {
            builder.HasData(

                     Category(1, "Programming"),
                     Category(2, "Fantasy"),
                     Category(3, "Fiction"),
                     Category(4, "Thriller"),
                     Category(5, "Finance"),
                     Category(6, "Romance"),
                     Category(7, "History"),
                     Category(8, "Technology"),
                     Category(9, "Economics"),
                     Category(10, "Comics"),
                     Category(11, "Business")
                           );

        }
    }
}
