using DevSync.Domain.Profiles;
using DevSync.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevSync.Infrastructure.Persistence.Configurations;

public sealed class DeveloperProfileConfiguration : IEntityTypeConfiguration<DeveloperProfile>
{
    public void Configure(EntityTypeBuilder<DeveloperProfile> builder)
    {
        builder.ToTable("DeveloperProfiles", table =>
            table.HasCheckConstraint("CK_DeveloperProfiles_LowercaseHandle", "\"Handle\" = lower(\"Handle\")"));
        builder.HasKey(profile => profile.Id);
        builder.Property(profile => profile.Handle).HasMaxLength(40).IsRequired();
        builder.Property(profile => profile.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(profile => profile.Bio).HasMaxLength(1000);
        builder.Property(profile => profile.Version).IsConcurrencyToken();
        builder.HasIndex(profile => profile.Handle).IsUnique();
        builder.HasIndex(profile => profile.UserId).IsUnique();
        builder.HasOne<ApplicationUser>().WithOne().HasForeignKey<DeveloperProfile>(profile => profile.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
