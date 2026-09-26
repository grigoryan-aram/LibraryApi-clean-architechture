using LibraryApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibraryApi.Infrastructure.Configurations
{
    public class PasswordResetCodesConfiguration
        : IEntityTypeConfiguration<PasswordResetCodeModel>
    {
        public void Configure(EntityTypeBuilder<PasswordResetCodeModel> builder)
        {
            builder.Property(c => c.IdentityUserId)
                .IsRequired()
                .HasMaxLength(450);

            // SHA-256 as hex.
            builder.Property(c => c.CodeHash)
                .IsRequired()
                .HasMaxLength(64);

            // Every read is "the live code for this user", so the lookup is
            // always by user id.
            builder.HasIndex(c => c.IdentityUserId);
        }
    }
}
