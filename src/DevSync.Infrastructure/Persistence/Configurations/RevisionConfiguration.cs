using DevSync.Domain.Revision;
using DevSync.Domain.StudyNotes;
using DevSync.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevSync.Infrastructure.Persistence.Configurations;

public sealed class RevisionConfiguration : IEntityTypeConfiguration<RevisionCard>, IEntityTypeConfiguration<RevisionReview>
{
    public void Configure(EntityTypeBuilder<RevisionCard> b)
    {
        b.ToTable("RevisionCards", t => t.HasCheckConstraint("CK_RevisionCards_Schedule",
            "\"IntervalDays\" BETWEEN 0 AND 365 AND \"ReviewCount\" >= 0 AND \"LapseCount\" >= 0 AND \"LapseCount\" <= \"ReviewCount\""));
        b.HasKey(c => c.Id);
        b.HasAlternateKey(c => new { c.Id, c.OwnerId });
        b.Property(c => c.Prompt).HasMaxLength(500);
        b.Property(c => c.Answer).HasMaxLength(4000);
        b.Property(c => c.Version).IsConcurrencyToken();
        b.HasIndex(c => new { c.OwnerId, c.DueAt, c.Id });
        b.HasOne<StudyNote>().WithMany().HasForeignKey(c => new { c.NoteId, c.OwnerId })
            .HasPrincipalKey(n => new { n.Id, n.OwnerId }).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(c => c.OwnerId).OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<RevisionReview> b)
    {
        b.ToTable("RevisionReviews", t => t.HasCheckConstraint("CK_RevisionReviews_GradeAndInterval",
            "\"Grade\" BETWEEN 0 AND 3 AND \"IntervalDays\" BETWEEN 0 AND 365 AND \"NextDueAt\" > \"ReviewedAt\""));
        b.HasKey(r => r.Id);
        b.HasIndex(r => new { r.CardId, r.SubmissionId }).IsUnique();
        b.HasIndex(r => new { r.CardId, r.SubmittedVersion }).IsUnique();
        b.HasIndex(r => new { r.OwnerId, r.ReviewedAt, r.Id });
        b.HasOne<RevisionCard>().WithMany().HasForeignKey(r => new { r.CardId, r.OwnerId })
            .HasPrincipalKey(c => new { c.Id, c.OwnerId }).OnDelete(DeleteBehavior.Cascade);
    }
}
