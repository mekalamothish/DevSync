using DevSync.Domain.Governance;
using DevSync.Domain.Blogs;
using DevSync.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace DevSync.Infrastructure.Persistence.Configurations;
public sealed class GovernanceConfiguration : IEntityTypeConfiguration<AccountPreferences>, IEntityTypeConfiguration<PublisherVerification>,
    IEntityTypeConfiguration<ContentReport>, IEntityTypeConfiguration<GovernanceAudit>
{
    public void Configure(EntityTypeBuilder<AccountPreferences> b)
    {
        b.ToTable("AccountPreferences",t=>t.HasCheckConstraint("CK_AccountPreferences_PageSize","\"ArticlesPageSize\" IN (12,24)"));
        b.HasKey(x=>x.UserId); b.Property(x=>x.Version).IsConcurrencyToken();
        b.HasOne<ApplicationUser>().WithOne().HasForeignKey<AccountPreferences>(x=>x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
    public void Configure(EntityTypeBuilder<PublisherVerification> b)
    {
        b.ToTable("PublisherVerifications",t=>t.HasCheckConstraint("CK_PublisherVerifications_Status","\"Status\" IN ('pending','approved','rejected','revoked')"));
        b.HasKey(x=>x.Id); b.Property(x=>x.Version).IsConcurrencyToken();
        b.Property(x=>x.EvidenceUrl).HasMaxLength(2048).IsRequired(); b.Property(x=>x.Statement).HasMaxLength(2000).IsRequired();
        b.Property(x=>x.Status).HasMaxLength(12).IsRequired(); b.Property(x=>x.DecisionReason).HasMaxLength(2000).IsRequired();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.ReviewerId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x=>x.UserId).IsUnique().HasFilter("\"Status\" IN ('pending','approved')");
        b.HasIndex(x=>new{x.Status,x.SubmittedAt});
    }
    public void Configure(EntityTypeBuilder<ContentReport> b)
    {
        b.ToTable("ContentReports",t=>{
            t.HasCheckConstraint("CK_ContentReports_Status","\"Status\" IN ('pending','resolved','dismissed')");
            t.HasCheckConstraint("CK_ContentReports_Kind","\"Kind\" IN ('post','comment')");
            t.HasCheckConstraint("CK_ContentReports_Category","\"Category\" IN ('spam','abuse','unsafe','copyright','other')");
        });
        b.HasKey(x=>x.Id); b.Property(x=>x.Version).IsConcurrencyToken();
        b.Property(x=>x.Kind).HasMaxLength(12); b.Property(x=>x.Category).HasMaxLength(12); b.Property(x=>x.Status).HasMaxLength(12);
        b.Property(x=>x.Details).HasMaxLength(2000); b.Property(x=>x.Excerpt).HasMaxLength(2000);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.ReporterId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<BlogPost>().WithMany().HasForeignKey(x=>x.PostId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<BlogComment>().WithMany().HasForeignKey(x=>x.CommentId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x=>new{x.ReporterId,x.Kind,x.TargetId}).IsUnique().HasFilter("\"Status\" = 'pending'");
        b.HasIndex(x=>new{x.Status,x.CreatedAt});
    }
    public void Configure(EntityTypeBuilder<GovernanceAudit> b)
    {
        b.ToTable("GovernanceAudits"); b.HasKey(x=>x.Id);
        b.Property(x=>x.Kind).HasMaxLength(20); b.Property(x=>x.Action).HasMaxLength(20); b.Property(x=>x.Reason).HasMaxLength(2000);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.ActorId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x=>new{x.Kind,x.TargetId,x.CreatedAt});
    }
}
