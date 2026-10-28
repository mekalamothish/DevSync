using DevSync.Domain.Teams;
using DevSync.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevSync.Infrastructure.Persistence.Configurations;

public sealed class TeamsConfiguration : IEntityTypeConfiguration<Team>, IEntityTypeConfiguration<TeamMembership>,
    IEntityTypeConfiguration<TeamInvitation>, IEntityTypeConfiguration<TeamWorkspaceItem>, IEntityTypeConfiguration<TeamActivity>
{
    public void Configure(EntityTypeBuilder<Team> b)
    {
        b.ToTable("Teams"); b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(80).IsRequired();
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.OwnerId);
    }
    public void Configure(EntityTypeBuilder<TeamMembership> b)
    {
        b.ToTable("TeamMemberships", t => t.HasCheckConstraint("CK_TeamMemberships_Role", "\"Role\" IN ('owner','admin','member','viewer')"));
        b.HasKey(x => new { x.TeamId, x.UserId }); b.Property(x => x.Role).HasMaxLength(12).IsRequired();
        b.HasOne<Team>().WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.TeamId).IsUnique().HasFilter("\"Role\" = 'owner'");
    }
    public void Configure(EntityTypeBuilder<TeamInvitation> b)
    {
        b.ToTable("TeamInvitations", t => t.HasCheckConstraint("CK_TeamInvitations_Role", "\"Role\" IN ('member','viewer')"));
        b.HasKey(x => x.Id); b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.Property(x => x.NormalizedEmail).HasMaxLength(256).IsRequired();
        b.Property(x => x.Role).HasMaxLength(12).IsRequired(); b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique(); b.HasIndex(x => new { x.TeamId, x.NormalizedEmail });
        b.HasOne<Team>().WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
    }
    public void Configure(EntityTypeBuilder<TeamWorkspaceItem> b)
    {
        b.ToTable("TeamWorkspaceItems", t => {
            t.HasCheckConstraint("CK_TeamWorkspaceItems_Kind", "\"Kind\" IN ('note','blog','task')");
            t.HasCheckConstraint("CK_TeamWorkspaceItems_State", "\"State\" IN ('todo','in-progress','done')");
            t.HasCheckConstraint("CK_TeamWorkspaceItems_Priority", "\"Priority\" IN ('low','medium','high','urgent')");
            t.HasCheckConstraint("CK_TeamWorkspaceItems_ContentFormat", "\"ContentFormat\" IN ('markdown','richtext')");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Kind).HasMaxLength(8).IsRequired(); b.Property(x => x.Title).HasMaxLength(160).IsRequired();
        b.Property(x => x.Body).HasMaxLength(50000).IsRequired(); b.Property(x => x.State).HasMaxLength(12).IsRequired();
        b.Property(x=>x.ContentFormat).HasMaxLength(12).HasDefaultValue("markdown");
        b.Property(x => x.Priority).HasMaxLength(12).IsRequired(); b.Property(x => x.Version).IsConcurrencyToken();
        b.HasOne<Team>().WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AssignedUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => new { x.TeamId, x.IsArchived, x.UpdatedAt });
    }
    public void Configure(EntityTypeBuilder<TeamActivity> b)
    {
        b.ToTable("TeamActivities"); b.HasKey(x => x.Id); b.Property(x => x.Description).HasMaxLength(300).IsRequired();
        b.HasOne<Team>().WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => new { x.TeamId, x.CreatedAt });
    }
}
