using DevSync.Domain.Blogs;
using DevSync.Domain.StudyNotes;
using DevSync.Domain.Tasks;
using DevSync.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevSync.Infrastructure.Persistence.Configurations;

public sealed class TasksConfiguration : IEntityTypeConfiguration<TaskBoard>, IEntityTypeConfiguration<TaskColumn>,
    IEntityTypeConfiguration<WorkTask>, IEntityTypeConfiguration<TaskChecklistItem>, IEntityTypeConfiguration<TaskActivity>
{
    public void Configure(EntityTypeBuilder<TaskBoard> b)
    {
        b.ToTable("TaskBoards"); b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.Id, x.OwnerId });
        b.Property(x => x.Name).HasMaxLength(80); b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => new { x.OwnerId, x.CreatedAt });
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
    }
    public void Configure(EntityTypeBuilder<TaskColumn> b)
    {
        b.ToTable("TaskColumns", t =>
        {
            t.HasCheckConstraint("CK_TaskColumns_Key", "\"Key\" IN ('todo', 'in-progress', 'done')");
            t.HasCheckConstraint("CK_TaskColumns_Position", "\"Position\" >= 0 AND \"Position\" <= 2");
        });
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.Id, x.BoardId, x.OwnerId });
        b.Property(x => x.Name).HasMaxLength(30); b.Property(x => x.Key).HasMaxLength(12);
        b.HasIndex(x => new { x.BoardId, x.Key }).IsUnique();
        b.HasOne<TaskBoard>().WithMany().HasForeignKey(x => new { x.BoardId, x.OwnerId })
            .HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.Cascade);
    }
    public void Configure(EntityTypeBuilder<WorkTask> b)
    {
        b.ToTable("WorkTasks", t =>
        {
            t.HasCheckConstraint("CK_WorkTasks_Priority", "\"Priority\" IN ('low', 'medium', 'high', 'urgent')");
            t.HasCheckConstraint("CK_WorkTasks_Assignment", "\"AssignedUserId\" IS NULL OR \"AssignedUserId\" = \"OwnerId\"");
            t.HasCheckConstraint("CK_WorkTasks_Position", "(\"IsArchived\" AND \"Position\" = -1) OR (NOT \"IsArchived\" AND \"Position\" >= 0 AND \"Position\" < 200)");
        });
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.Id, x.OwnerId });
        b.Property(x => x.Title).HasMaxLength(160); b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.Priority).HasMaxLength(12); b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => new { x.BoardId, x.IsArchived, x.ColumnId, x.Position });
        b.HasIndex(x => new { x.OwnerId, x.IsArchived, x.DueDate });
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<TaskBoard>().WithMany().HasForeignKey(x => new { x.BoardId, x.OwnerId })
            .HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<TaskColumn>().WithMany().HasForeignKey(x => new { x.ColumnId, x.BoardId, x.OwnerId })
            .HasPrincipalKey(x => new { x.Id, x.BoardId, x.OwnerId }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AssignedUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<StudyNote>().WithMany().HasForeignKey(x => new { x.LinkedNoteId, x.OwnerId })
            .HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<BlogPost>().WithMany().HasForeignKey(x => x.LinkedPostId).OnDelete(DeleteBehavior.SetNull);
    }
    public void Configure(EntityTypeBuilder<TaskChecklistItem> b)
    {
        b.ToTable("TaskChecklistItems", t => t.HasCheckConstraint("CK_TaskChecklistItems_Position", "\"Position\" >= 0 AND \"Position\" < 30"));
        b.HasKey(x => x.Id); b.Property(x => x.Text).HasMaxLength(300);
        b.HasIndex(x => new { x.TaskId, x.Position });
        b.HasOne<WorkTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
    }
    public void Configure(EntityTypeBuilder<TaskActivity> b)
    {
        b.ToTable("TaskActivities"); b.HasKey(x => x.Id); b.Property(x => x.Description).HasMaxLength(300);
        b.HasIndex(x => new { x.TaskId, x.CreatedAt, x.Id });
        b.HasOne<WorkTask>().WithMany().HasForeignKey(x => new { x.TaskId, x.OwnerId })
            .HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.Cascade);
    }
}
