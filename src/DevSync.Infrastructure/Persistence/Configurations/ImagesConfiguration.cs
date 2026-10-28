using DevSync.Domain.Images;
using DevSync.Domain.Teams;
using DevSync.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevSync.Infrastructure.Persistence.Configurations;

public sealed class ImagesConfiguration : IEntityTypeConfiguration<ImageAsset>
{
    public void Configure(EntityTypeBuilder<ImageAsset> b)
    {
        b.ToTable("ImageAssets", t => {
            t.HasCheckConstraint("CK_ImageAssets_Scope", "(\"OwnerId\" IS NULL) <> (\"TeamId\" IS NULL)");
            t.HasCheckConstraint("CK_ImageAssets_Content", "\"ContentType\" IN ('image/png','image/jpeg') AND octet_length(\"Data\") BETWEEN 1 AND 5242880 AND \"Width\" > 0 AND \"Height\" > 0 AND \"Width\"::bigint * \"Height\" <= 20000000");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(160).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(20).IsRequired();
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Team>().WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.OwnerId, x.CreatedAt, x.Id });
        b.HasIndex(x => new { x.TeamId, x.CreatedAt, x.Id });
    }
}
