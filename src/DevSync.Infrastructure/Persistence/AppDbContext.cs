using DevSync.Domain.Profiles;
using DevSync.Domain.Blogs;
using DevSync.Domain.StudyNotes;
using DevSync.Domain.Revision;
using DevSync.Domain.Tasks;
using DevSync.Domain.Teams;
using DevSync.Domain.Governance;
using DevSync.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DevSync.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<DevSync.Domain.Images.ImageAsset> ImageAssets => Set<DevSync.Domain.Images.ImageAsset>();
    public DbSet<DeveloperProfile> DeveloperProfiles => Set<DeveloperProfile>();
    public DbSet<BlogPost> BlogPosts => Set<BlogPost>();
    public DbSet<BlogTag> BlogTags => Set<BlogTag>();
    public DbSet<BlogBookmark> BlogBookmarks => Set<BlogBookmark>();
    public DbSet<BlogReaction> BlogReactions => Set<BlogReaction>();
    public DbSet<BlogComment> BlogComments => Set<BlogComment>();
    public DbSet<StudyNotebook> StudyNotebooks => Set<StudyNotebook>();
    public DbSet<StudyNote> StudyNotes => Set<StudyNote>();
    public DbSet<StudyNoteBlock> StudyNoteBlocks => Set<StudyNoteBlock>();
    public DbSet<StudyNoteTag> StudyNoteTags => Set<StudyNoteTag>();
    public DbSet<RevisionCard> RevisionCards => Set<RevisionCard>();
    public DbSet<RevisionReview> RevisionReviews => Set<RevisionReview>();
    public DbSet<TaskBoard> TaskBoards => Set<TaskBoard>();
    public DbSet<TaskColumn> TaskColumns => Set<TaskColumn>();
    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
    public DbSet<TaskChecklistItem> TaskChecklistItems => Set<TaskChecklistItem>();
    public DbSet<TaskActivity> TaskActivities => Set<TaskActivity>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMembership> TeamMemberships => Set<TeamMembership>();
    public DbSet<TeamInvitation> TeamInvitations => Set<TeamInvitation>();
    public DbSet<TeamWorkspaceItem> TeamWorkspaceItems => Set<TeamWorkspaceItem>();
    public DbSet<TeamActivity> TeamActivities => Set<TeamActivity>();

    public DbSet<AccountPreferences> AccountPreferences => Set<AccountPreferences>();
    public DbSet<PublisherVerification> PublisherVerifications => Set<PublisherVerification>();
    public DbSet<ContentReport> ContentReports => Set<ContentReport>();
    public DbSet<GovernanceAudit> GovernanceAudits => Set<GovernanceAudit>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ApplicationUser>().HasIndex(user => user.NormalizedEmail)
            .HasDatabaseName("EmailIndex").IsUnique();
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
