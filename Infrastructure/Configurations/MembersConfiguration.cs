using LibraryApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibraryApi.Infrastructure.Configurations
{
    public class MembersConfiguration : IEntityTypeConfiguration<MemberModel>
    {
        public void Configure(EntityTypeBuilder<MemberModel> builder)
        {
            // No HasData here, deliberately.
            //
            // This class used to build ten MemberModel objects and throw every
            // one of them away — the return values were never passed to
            // HasData, so nothing was ever seeded and no migration ever
            // inserted a member. Simply adding the HasData call now would be
            // worse than the bug: databases in use already hold member rows at
            // ids 1..n, and seed rows are inserted by primary key, so the
            // migration would fail with a duplicate-key violation on startup —
            // and Program.cs runs Migrate() before the app serves anything.
            //
            // Members are people who joined a library, not demo data. They are
            // created through POST /api/Members or the /members page.
        }
    }
}
