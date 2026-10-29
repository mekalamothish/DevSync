using DevSync.Domain.Blogs;
using DevSync.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevSync.Infrastructure.Persistence.Configurations;

public sealed class BlogConfiguration : IEntityTypeConfiguration<BlogPost>, IEntityTypeConfiguration<BlogTag>,
    IEntityTypeConfiguration<BlogBookmark>, IEntityTypeConfiguration<BlogReaction>, IEntityTypeConfiguration<BlogComment>
{
    public void Configure(EntityTypeBuilder<BlogPost> b)
    {
        b.ToTable("BlogPosts", t => t.HasCheckConstraint("CK_BlogPosts_PublicSnapshot",
            "(\"PublishedAt\" IS NULL AND \"Slug\" IS NULL AND \"PublishedTitle\" IS NULL AND \"PublishedSummary\" IS NULL AND \"PublishedMarkdown\" IS NULL) OR (\"PublishedAt\" IS NOT NULL AND \"Slug\" IS NOT NULL AND \"PublishedTitle\" IS NOT NULL AND \"PublishedSummary\" IS NOT NULL AND \"PublishedMarkdown\" IS NOT NULL)"));
        b.HasKey(p => p.Id);
        b.Property(p => p.Title).HasMaxLength(160);
        b.Property(p => p.Summary).HasMaxLength(500);
        b.Property(p => p.Markdown).HasMaxLength(60000);
        b.Property(p => p.EditorFormat).HasMaxLength(12).HasDefaultValue("markdown");
        b.Property(p => p.BlocksJson).HasColumnType("text").HasDefaultValue("[]");
        b.Property(p => p.RichText).HasMaxLength(60000).HasDefaultValue("");
        b.Property(p => p.PublishedEditorFormat).HasMaxLength(12).HasDefaultValue("markdown");
        b.Property(p => p.PublishedBlocksJson).HasColumnType("text").HasDefaultValue("[]");
        b.Property(p => p.PublishedRichText).HasMaxLength(60000).HasDefaultValue("");
        b.ToTable(t=>t.HasCheckConstraint("CK_BlogPosts_EditorFormats","\"EditorFormat\" IN ('markdown','blocks','richtext') AND \"PublishedEditorFormat\" IN ('markdown','blocks','richtext')"));
        b.HasOne<DevSync.Domain.Images.ImageAsset>().WithMany().HasForeignKey(x => x.CoverImageId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<DevSync.Domain.Images.ImageAsset>().WithMany().HasForeignKey(x => x.PublishedCoverImageId).OnDelete(DeleteBehavior.SetNull);
        b.Property(p => p.DraftTags).HasMaxLength(154);
        b.Property(p => p.PublishedTitle).HasMaxLength(160);
        b.Property(p => p.PublishedSummary).HasMaxLength(500);
        b.Property(p => p.PublishedMarkdown).HasMaxLength(60000);
        b.Property(p => p.Slug).HasMaxLength(150);
        b.Property(p => p.Version).IsConcurrencyToken();
        b.HasIndex(p => p.Slug).IsUnique();
        b.HasIndex(p => new { p.PublishedAt, p.Id });
        b.HasIndex(p => new { p.AuthorId, p.UpdatedAt });
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.AuthorId).OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<BlogTag> b)
    {
        b.HasKey(t => new { t.PostId, t.Name });
        b.Property(t => t.Name).HasMaxLength(30);
        b.HasIndex(t => t.Name);
        b.HasOne<BlogPost>().WithMany().HasForeignKey(t => t.PostId).OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<BlogBookmark> b)
    {
        b.HasKey(x => new { x.PostId, x.UserId });
        b.HasIndex(x => x.UserId);
        b.HasOne<BlogPost>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<BlogReaction> b)
    {
        b.HasKey(x => new { x.PostId, x.UserId });
        b.HasOne<BlogPost>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<BlogComment> b)
    {
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasKey(x => x.Id);
        b.Property(x => x.Body).HasMaxLength(2000);
        b.HasIndex(x => new { x.PostId, x.CreatedAt, x.Id });
        b.HasOne<BlogPost>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
