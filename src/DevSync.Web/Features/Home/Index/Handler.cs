using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevSync.Web.Features.Home.Index;

public sealed class Handler(IServiceProvider services, IConfiguration configuration, TimeProvider clock)
{
    public async Task<ViewModel> HandleAsync(Guid? userId, CancellationToken ct)
    {
        var model = new ViewModel(
        "Your next idea starts here.",
        "A home for the things you build, the knowledge you collect, and the people you learn with.",
        [
            new("01", "Share what you know", "Turn your experience into technical articles, with room for code and the details that matter.", "Technical blogs", "/blogs"),
            new("02", "Make learning stick", "Keep study notes together and revisit the ideas you want to remember.", "Notes & revision", "/notes"),
            new("03", "Move ideas forward", "Plan your next steps, track progress on a board, and connect tasks with your knowledge.", "Tasks & Kanban", "/tasks")
        ]);
        // The foundation preview works without configuration. A configured database
        // is queried normally; failures are surfaced rather than disguised as empty data.
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection"))) return model;
        var feed = await services.GetRequiredService<DevSync.Web.Features.Blogs.Explore.Handler>().HandleAsync(null, null, 1, null, ct);
        model = model with { Articles = feed.Articles.Take(3).ToArray() };
        if (userId is not null)
        {
            var db = services.GetRequiredService<AppDbContext>();
            var posts = db.BlogPosts.Where(p => p.AuthorId == userId.Value);
            var now = clock.GetUtcNow();
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            var openTasks = db.WorkTasks.Where(t => t.OwnerId == userId.Value && !t.IsArchived && t.CompletedAt == null);
            var due = from card in db.RevisionCards
                join note in db.StudyNotes on card.NoteId equals note.Id
                where card.OwnerId == userId.Value && note.OwnerId == userId.Value && !note.IsArchived && card.DueAt <= now
                select card.Id;
            model = model with { Drafts = await posts.CountAsync(p => p.PublishedAt == null, ct),
                Published = await posts.CountAsync(p => p.PublishedAt != null, ct),
                ActiveNotes = await db.StudyNotes.CountAsync(n => n.OwnerId == userId.Value && !n.IsArchived, ct),
                DueReviews = await due.CountAsync(ct),
                OpenTasks = await openTasks.CountAsync(ct),
                OverdueTasks = await openTasks.CountAsync(t => t.DueDate < today, ct) };
        }
        return model;
    }
}
