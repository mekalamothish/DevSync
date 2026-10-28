using DevSync.Domain.Blogs;
using DevSync.Domain.StudyNotes;
using DevSync.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevSync.Infrastructure.Persistence.Configurations;

public sealed class StudyNotesConfiguration : IEntityTypeConfiguration<StudyNotebook>, IEntityTypeConfiguration<StudyNote>,
    IEntityTypeConfiguration<StudyNoteBlock>, IEntityTypeConfiguration<StudyNoteTag>
{
    public void Configure(EntityTypeBuilder<StudyNotebook> b)
    {
        b.ToTable("StudyNotebooks");
        b.HasKey(n => n.Id);
        b.HasAlternateKey(n => new { n.Id, n.OwnerId });
        b.Property(n => n.Name).HasMaxLength(80);
        b.Property(n => n.NormalizedName).HasMaxLength(80);
        b.HasIndex(n => new { n.OwnerId, n.NormalizedName }).IsUnique();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(n => n.OwnerId).OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<StudyNote> b)
    {
        b.ToTable("StudyNotes");
        b.HasKey(n => n.Id);
        b.HasAlternateKey(n => new { n.Id, n.OwnerId });
        b.Property(n => n.Title).HasMaxLength(160);
        b.Property(n => n.Version).IsConcurrencyToken();
        b.HasIndex(n => new { n.OwnerId, n.IsArchived, n.UpdatedAt });
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(n => n.OwnerId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<StudyNotebook>().WithMany().HasForeignKey(n => new { n.NotebookId, n.OwnerId })
            .HasPrincipalKey(n => new { n.Id, n.OwnerId }).OnDelete(DeleteBehavior.NoAction);
    }

    public void Configure(EntityTypeBuilder<StudyNoteBlock> b)
    {
        b.ToTable("StudyNoteBlocks", t =>
        {
            t.HasCheckConstraint("CK_StudyNoteBlocks_Position", "\"Position\" >= 0 AND \"Position\" < 50");
            t.HasCheckConstraint("CK_StudyNoteBlocks_Kind", "\"Kind\" IN ('paragraph', 'heading', 'code', 'checklist', 'article', 'richtext')");
            t.HasCheckConstraint("CK_StudyNoteBlocks_Article", "(\"Kind\" = 'article') OR \"LinkedPostId\" IS NULL");
        });
        b.HasKey(n => n.Id);
        b.Property(n => n.Kind).HasMaxLength(12);
        b.Property(n => n.Content).HasMaxLength(60000);
        b.Property(n => n.Language).HasMaxLength(30);
        b.HasIndex(n => new { n.NoteId, n.Position });
        b.HasOne<StudyNote>().WithMany().HasForeignKey(n => n.NoteId).OnDelete(DeleteBehavior.Cascade);
        // A removed article leaves the personal note intact, with an unavailable link.
        b.HasOne<BlogPost>().WithMany().HasForeignKey(n => n.LinkedPostId).OnDelete(DeleteBehavior.SetNull);
    }

    public void Configure(EntityTypeBuilder<StudyNoteTag> b)
    {
        b.ToTable("StudyNoteTags");
        b.HasKey(n => new { n.NoteId, n.Name });
        b.Property(n => n.Name).HasMaxLength(30);
        b.HasIndex(n => n.Name);
        b.HasOne<StudyNote>().WithMany().HasForeignKey(n => n.NoteId).OnDelete(DeleteBehavior.Cascade);
    }
}
