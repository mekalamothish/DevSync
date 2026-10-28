using System.ComponentModel.DataAnnotations;
using DevSync.Domain.Teams;
using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace DevSync.Web.Features.Teams.Workspace;

public sealed class Command
{
    public Guid Version { get; set; }
    [Required, StringLength(160)] public string Title { get; set; } = "";
    [StringLength(50000)] public string? Body { get; set; }
    [Required,StringLength(12)] public string ContentFormat {get;set;}="markdown";
    public string Kind { get; set; } = "note";
    public string State { get; set; } = "todo";
    public string Priority { get; set; } = "medium";
    public Guid? AssignedUserId { get; set; }
    public DateOnly? DueDate { get; set; }
}
public sealed record Source(Guid Id, string Kind, string Title);
public sealed record ItemRow(Guid Id, string Title, string Kind, string State, string Priority, DateOnly? DueDate, string? AssignedName);
public sealed record ViewModel(Manage.ViewModel Team, ItemRow[] Items, Source[] Sources, string Kind, bool Archived, int Page, int Pages, int Total);
public sealed record ItemModel(Guid Id, Manage.ViewModel Team, Command Command, bool IsArchived, DateTimeOffset UpdatedAt);
public sealed class Handler(AppDbContext db, Manage.Handler manage, TimeProvider clock)
{
    public async Task<ViewModel?> LoadAsync(Guid id, Guid actor, string? kind, bool archived, int page, CancellationToken ct)
    {
        var team = await manage.LoadAsync(id, actor, ct); if (team is null) return null;
        kind = kind is "note" or "blog" or "task" ? kind : "all";
        var query = db.TeamWorkspaceItems.AsNoTracking().Where(x => x.TeamId == id && x.IsArchived == archived);
        if (kind != "all") query = query.Where(x => x.Kind == kind);
        var total = await query.CountAsync(ct); var pages = Math.Max(1, (total + 19) / 20); page = Math.Clamp(page, 1, pages);
        var items = await query.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id).Skip((page - 1) * 20).Take(20)
            .Select(x => new ItemRow(x.Id, x.Title, x.Kind, x.State, x.Priority, x.DueDate,
                db.DeveloperProfiles.Where(p => p.UserId == x.AssignedUserId).Select(p => p.DisplayName).FirstOrDefault())).ToArrayAsync(ct);
        var sources = new List<Source>();
        if (Access.Write(team.Role))
        {
            sources.AddRange(await db.StudyNotes.AsNoTracking().Where(n => n.OwnerId == actor && !n.IsArchived).OrderByDescending(n => n.UpdatedAt).Take(100).Select(n => new Source(n.Id, "note", n.Title)).ToArrayAsync(ct));
            sources.AddRange(await db.BlogPosts.AsNoTracking().Where(p => p.AuthorId == actor).OrderByDescending(p => p.UpdatedAt).Take(100).Select(p => new Source(p.Id, "blog", p.Title)).ToArrayAsync(ct));
            sources.AddRange(await db.WorkTasks.AsNoTracking().Where(t => t.OwnerId == actor && !t.IsArchived).OrderByDescending(t => t.UpdatedAt).Take(100).Select(t => new Source(t.Id, "task", t.Title)).ToArrayAsync(ct));
        }
        return new(team, items, sources.ToArray(), kind, archived, page, pages, total);
    }
    public async Task<ItemModel?> ItemAsync(Guid teamId, Guid id, Guid actor, CancellationToken ct)
    {
        var team = await manage.LoadAsync(teamId, actor, ct); if (team is null) return null;
        var item = await db.TeamWorkspaceItems.AsNoTracking().SingleOrDefaultAsync(x => x.TeamId == teamId && x.Id == id, ct);
        if (item is null) return null;
        return new(id, team, new Command { Version = item.Version, Title = item.Title, Body = item.Body, Kind = item.Kind,ContentFormat=item.ContentFormat,
            State = item.State, Priority = item.Priority, AssignedUserId = item.AssignedUserId, DueDate = item.DueDate }, item.IsArchived, item.UpdatedAt);
    }
    public async Task<Result> SaveAsync(Guid teamId, Guid? itemId, Guid actor, Command command, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (team, role) = await Access.LockAsync(db, teamId, actor, ct);
        if (team is null) return new(404);
        if (!Access.Write(role!)) return new(403, "Viewers can read team content but cannot change it.");
        var item = itemId is null ? new TeamWorkspaceItem { TeamId = teamId, CreatedById = actor, Kind = command.Kind } :
            await db.TeamWorkspaceItems.SingleOrDefaultAsync(x => x.Id == itemId && x.TeamId == teamId, ct);
        if (item is null) return new(404);
        if (itemId is not null && item.Version != command.Version) return new(409, "This item changed. Copy your work before reloading.");
        if (item.IsArchived) return new(422, "Restore the item before editing.");
        if (itemId is null && await db.TeamWorkspaceItems.CountAsync(x => x.TeamId == teamId, ct) >= 500) return new(422, "This team has reached its 500-item workspace limit.");
        var error = await ValidateAsync(teamId, item.Kind, command, ct); if (error is not null) return new(422, error);
        item.Title = command.Title.Trim(); item.Body = command.Body ?? "";
        item.ContentFormat=command.ContentFormat;
        if (item.Kind == "task") { item.State = command.State; item.Priority = command.Priority; item.AssignedUserId = command.AssignedUserId; item.DueDate = command.DueDate; }
        item.Version = Guid.NewGuid(); item.UpdatedAt = clock.GetUtcNow().ToUniversalTime();
        if (itemId is null) db.TeamWorkspaceItems.Add(item);
        Access.Record(db, team, actor, itemId is null ? "A team " + item.Kind + " was created." : "A team " + item.Kind + " was updated.", clock);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(200, Id: item.Id);
    }
    private async Task<string?> ValidateAsync(Guid teamId, string kind, Command c, CancellationToken ct)
    {
        if(c.ContentFormat is not ("markdown" or "richtext")) return "Choose a supported content format.";
        if(c.ContentFormat=="richtext" && DevSync.Web.Common.Content.RichTextDocument.Validate(c.Body) is string error) return error;
        if (kind is not ("note" or "blog" or "task") || string.IsNullOrWhiteSpace(c.Title) || c.Title.Trim().Length > 160 || (c.Body?.Length ?? 0) > 50000)
            return "Choose a valid type, a title of 1–160 characters, and content up to 50,000 characters.";
        if (kind == "task")
        {
            if (c.State is not ("todo" or "in-progress" or "done") || c.Priority is not ("low" or "medium" or "high" or "urgent")) return "Choose a valid task status and priority.";
            if (c.AssignedUserId is not null && !await db.TeamMemberships.AnyAsync(m => m.TeamId == teamId && m.UserId == c.AssignedUserId && m.Role != "viewer", ct))
                return "Assign tasks to a current owner, admin, or member of this team.";
        }
        return null;
    }
    public async Task<Result> ImportAsync(Guid teamId, Guid actor, string? kind, Guid sourceId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (team, role) = await Access.LockAsync(db, teamId, actor, ct);
        if (team is null) return new(404);
        if (!Access.Write(role!)) return new(403, "Viewers cannot share content.");
        if (await db.TeamWorkspaceItems.CountAsync(x => x.TeamId == teamId, ct) >= 500) return new(422, "This team has reached its 500-item workspace limit.");
        var item = new TeamWorkspaceItem { TeamId = teamId, CreatedById = actor, Kind = kind ?? "" };
        if (kind == "blog")
        {
            var p = await db.BlogPosts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sourceId && x.AuthorId == actor, ct);
            if (p is null) return new(404); item.Title = p.Title;
            item.ContentFormat=p.EditorFormat=="markdown"?"markdown":"richtext";
            item.Body=p.EditorFormat switch {"richtext"=>p.RichText,"blocks"=>DevSync.Web.Features.Blogs.Common.BlogDocument.ToRichText(DevSync.Web.Features.Blogs.Common.BlogDocument.ReadBlocks(p.BlocksJson)),_=>p.Markdown};
        }
        else if (kind == "note")
        {
            var n = await db.StudyNotes.FromSqlInterpolated($"SELECT * FROM \"StudyNotes\" WHERE \"Id\" = {sourceId} AND \"OwnerId\" = {actor} AND NOT \"IsArchived\" FOR SHARE").AsNoTracking().SingleOrDefaultAsync(ct);
            if (n is null) return new(404); item.Title = n.Title;
            var blocks = await db.StudyNoteBlocks.AsNoTracking().Where(b => b.NoteId == n.Id).OrderBy(b => b.Position).ToListAsync(ct);
            // Personal article links are omitted; their private targets never travel with a copy.
            item.Body = string.Join("\n\n", blocks.Where(b => b.Kind != "article").Select(b => b.Kind switch
            { "heading" => "## " + b.Content, "code" => "```\n" + b.Content + "\n```", "checklist" => "- " + b.Content, _ => b.Content }));
            if(blocks.Any(b=>b.Kind=="richtext"))
            {
                item.ContentFormat="richtext";
                item.Body=DevSync.Web.Features.Blogs.Common.BlogDocument.ToRichText(blocks.Select(b=>new DevSync.Web.Features.Notes.Edit.BlockCommand{Kind=b.Kind,Content=b.Content,Language=b.Language}));
            }
        }
        else if (kind == "task")
        {
            var t = await db.WorkTasks.FromSqlInterpolated($"SELECT * FROM \"WorkTasks\" WHERE \"Id\" = {sourceId} AND \"OwnerId\" = {actor} AND NOT \"IsArchived\" FOR SHARE").AsNoTracking().SingleOrDefaultAsync(ct);
            if (t is null) return new(404);
            item.Title = t.Title; item.Body = t.Description; item.Priority = t.Priority; item.DueDate = t.DueDate;
            item.State = t.CompletedAt is null ? "todo" : "done";
            var checklist = await db.TaskChecklistItems.AsNoTracking().Where(x => x.TaskId == t.Id).OrderBy(x => x.Position).ToListAsync(ct);
            if (checklist.Count > 0) item.Body += "\n\n" + string.Join("\n", checklist.Select(x => "- [" + (x.IsCompleted ? "x" : " ") + "] " + x.Text));
        }
        else return new(422, "Choose a note, article, or task.");
        if (item.Title.Length > 160 || item.Body.Length > 50000) return new(422, "This content exceeds the team-copy limits: 160 title and 50,000 content characters.");
        if(item.ContentFormat=="richtext" && DevSync.Web.Common.Content.RichTextDocument.Validate(item.Body) is string documentError) return new(422,documentError);
        item.UpdatedAt = clock.GetUtcNow().ToUniversalTime(); db.TeamWorkspaceItems.Add(item);
        Access.Record(db, team, actor, "A personal " + kind + " was copied into the team workspace.", clock);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(200, Id: item.Id);
    }
    public async Task<Result> ArchiveAsync(Guid teamId, Guid id, Guid actor, Guid version, bool archived, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (team, role) = await Access.LockAsync(db, teamId, actor, ct);
        if (team is null) return new(404);
        if (!Access.Write(role!)) return new(403, "Viewers cannot archive content.");
        var item = await db.TeamWorkspaceItems.SingleOrDefaultAsync(x => x.TeamId == teamId && x.Id == id, ct);
        if (item is null) return new(404);
        if (item.Version != version) return new(409, "This item changed. Reload before changing its archive state.");
        item.IsArchived = archived; item.Version = Guid.NewGuid(); item.UpdatedAt = clock.GetUtcNow().ToUniversalTime();
        Access.Record(db, team, actor, archived ? "A team item was archived." : "A team item was restored.", clock);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(200, Id: id);
    }
}
