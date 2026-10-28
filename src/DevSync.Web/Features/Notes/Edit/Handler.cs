using System.Text.RegularExpressions;
using DevSync.Domain.StudyNotes;
using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DevSync.Web.Features.Notes.Edit;

public sealed record SaveResult(int Status, string? Error, Guid Version);
public sealed record CreateResult(Guid? Id, string? Error);

public sealed class Handler(AppDbContext db)
{
    public static readonly string[] Kinds = ["paragraph", "heading", "code", "checklist", "article", "richtext"];
    private const string Conflict = "This note changed in another tab. Copy your work before reloading the latest version.";

    public async Task<Command?> LoadAsync(Guid id, Guid ownerId, CancellationToken ct)
    {
        var note = await db.StudyNotes.AsNoTracking().SingleOrDefaultAsync(n => n.Id == id && n.OwnerId == ownerId, ct);
        if (note is null) return null;
        var command = new Command
        {
            Id = note.Id, Version = note.Version, Title = note.Title, NotebookId = note.NotebookId,
            Tags = string.Join(',', await db.StudyNoteTags.AsNoTracking().Where(t => t.NoteId == id).OrderBy(t => t.Name).Select(t => t.Name).ToListAsync(ct)),
            Blocks = await db.StudyNoteBlocks.AsNoTracking().Where(b => b.NoteId == id).OrderBy(b => b.Position).ThenBy(b => b.Id)
                .Select(b => new BlockCommand { Id = b.Id, Kind = b.Kind, Content = b.Content, Language = b.Language, LinkedPostId = b.LinkedPostId }).ToListAsync(ct),
            IsArchived = note.IsArchived, UpdatedAt = note.UpdatedAt
        };
        await PopulateChoicesAsync(command, ownerId, ct);
        return command;
    }

    public async Task PopulateChoicesAsync(Command command, Guid ownerId, CancellationToken ct)
    {
        command.Notebooks = await db.StudyNotebooks.AsNoTracking().Where(n => n.OwnerId == ownerId)
            .OrderBy(n => n.NormalizedName).Select(n => new NotebookChoice(n.Id, n.Name)).ToListAsync(ct);
        // Public article choices use their published snapshot. Owners may link their working drafts.
        command.Articles = await db.BlogPosts.AsNoTracking().Where(p => p.AuthorId == ownerId || (p.PublishedAt != null && !p.IsHidden))
            .OrderByDescending(p => p.UpdatedAt).ThenByDescending(p => p.Id)
            .Take(200).Select(p => new ArticleChoice(p.Id, p.AuthorId == ownerId ? p.Title : p.PublishedTitle!,
                p.AuthorId == ownerId ? "/blogs/" + p.Id + "/preview" : "/blogs/" + p.Slug, p.AuthorId == ownerId)).ToListAsync(ct);
        var selectedIds = command.Blocks.Where(b => b.LinkedPostId is not null).Select(b => b.LinkedPostId!.Value).Distinct().ToArray();
        var missingIds = selectedIds.Except(command.Articles.Select(p => p.Id)).ToArray();
        if (missingIds.Length == 0) return;
        var extra = await db.BlogPosts.AsNoTracking().Where(p => missingIds.Contains(p.Id) && (p.AuthorId == ownerId || (p.PublishedAt != null && !p.IsHidden)))
            .Select(p => new ArticleChoice(p.Id, p.AuthorId == ownerId ? p.Title : p.PublishedTitle!,
                p.AuthorId == ownerId ? "/blogs/" + p.Id + "/preview" : "/blogs/" + p.Slug, p.AuthorId == ownerId)).ToListAsync(ct);
        command.Articles = command.Articles.Concat(extra).ToList();
    }

    public async Task<CreateResult> CreateAsync(Guid ownerId, Guid? notebookId, CancellationToken ct)
    {
        if (notebookId is not null && !await db.StudyNotebooks.AnyAsync(n => n.Id == notebookId && n.OwnerId == ownerId, ct))
            return new(null, "Choose one of your notebooks.");
        var note = new StudyNote { OwnerId = ownerId, NotebookId = notebookId };
        db.StudyNotes.Add(note);
        db.StudyNoteBlocks.Add(new StudyNoteBlock { NoteId = note.Id, Position = 0 });
        await db.SaveChangesAsync(ct);
        return new(note.Id, null);
    }

    public async Task<CreateResult> CreateNotebookAsync(Guid ownerId, string? name, CancellationToken ct)
    {
        name = (name ?? "").Trim();
        if (name.Length is < 1 or > 80) return new(null, "Name your notebook using 1–80 characters.");
        var normalizedName = name.ToLowerInvariant();
        if (await db.StudyNotebooks.AnyAsync(n => n.OwnerId == ownerId && n.NormalizedName == normalizedName, ct))
            return new(null, "You already have a notebook with that name.");
        var notebook = new StudyNotebook { OwnerId = ownerId, Name = name, NormalizedName = normalizedName };
        db.StudyNotebooks.Add(notebook);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { return new(null, "You already have a notebook with that name."); }
        return new(notebook.Id, null);
    }

    public async Task<SaveResult> HandleAsync(Guid id, Guid ownerId, Command command, string? operation, Guid? blockId, string? kind, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Lock the aggregate before its child rows, sharing the same order as revision review and archive.
        var note = await db.StudyNotes.FromSqlInterpolated($"SELECT * FROM \"StudyNotes\" WHERE \"Id\" = {id} AND \"OwnerId\" = {ownerId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (note is null) return new(404, "Note not found.", Guid.Empty);
        if (note.Version != command.Version) return new(409, Conflict, note.Version);
        if (note.IsArchived) return new(422, "Restore this archived note before editing it.", note.Version);
        var tags = (command.Tags ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.ToLowerInvariant()).Distinct().Order().ToArray();
        if (tags.Length > 5 || tags.Any(t => !Regex.IsMatch(t, "^[a-z0-9][a-z0-9+#.-]{0,29}$")))
            return new(422, "Use up to five comma-separated tags, each 1–30 letters, numbers, +, #, dot, or hyphen.", note.Version);
        if (string.IsNullOrWhiteSpace(command.Title) || command.Title.Length > 160)
            return new(422, "Use a note title between 1 and 160 characters.", note.Version);
        if (command.Blocks.Count > 50 || command.Blocks.Sum(b => (long)(b.Content?.Length ?? 0)) > 60000)
            return new(422, "Keep notes within 50 blocks and 60,000 content characters.", note.Version);
        if (command.Blocks.Any(b => !Kinds.Contains(b.Kind) || (b.Language?.Length ?? 0) > 30))
            return new(422, "Choose a supported block type and a code language up to 30 characters.", note.Version);
        foreach(var rich in command.Blocks.Where(b=>b.Kind=="richtext"))
            if(DevSync.Web.Common.Content.RichTextDocument.Validate(rich.Content) is string richError) return new(422,richError,note.Version);
        if (command.NotebookId is not null && !await db.StudyNotebooks.AnyAsync(n => n.Id == command.NotebookId && n.OwnerId == ownerId, ct))
            return new(422, "Choose one of your notebooks.", note.Version);
        var existingBlocks = await db.StudyNoteBlocks.Where(b => b.NoteId == id).ToListAsync(ct);
        foreach (var block in command.Blocks.Where(b => b.Id == Guid.Empty)) block.Id = Guid.NewGuid();
        var blockIds = command.Blocks.Select(b => b.Id).ToArray();
        if (blockIds.Distinct().Count() != blockIds.Length || await db.StudyNoteBlocks.AnyAsync(b => b.NoteId != id && blockIds.Contains(b.Id), ct))
            return new(422, "Each block must belong to this note and appear only once.", note.Version);
        var blocks = command.Blocks.ToList();
        Guid? addedPlaceholder = null;
        if (!string.IsNullOrEmpty(operation))
        {
            var index = blocks.FindIndex(b => b.Id == blockId);
            switch (operation)
            {
                case "add" when Kinds.Contains(kind ?? "") && blocks.Count < 50:
                    addedPlaceholder = Guid.NewGuid();
                    blocks.Add(new BlockCommand { Id = addedPlaceholder.Value, Kind = kind! });
                    break;
                case "remove" when index >= 0:
                    blocks.RemoveAt(index);
                    break;
                case "up" when index > 0:
                    (blocks[index - 1], blocks[index]) = (blocks[index], blocks[index - 1]);
                    break;
                case "down" when index >= 0 && index + 1 < blocks.Count:
                    (blocks[index + 1], blocks[index]) = (blocks[index], blocks[index + 1]);
                    break;
                default:
                    return new(422, "Choose a valid block action. Notes can contain up to 50 blocks.", note.Version);
            }
        }
        var linkedIds = blocks.Where(b => b.Kind == "article" && b.LinkedPostId is not null).Select(b => b.LinkedPostId!.Value).Distinct().ToArray();
        var allowedLinks = await db.BlogPosts.AsNoTracking().Where(p => linkedIds.Contains(p.Id) && (p.AuthorId == ownerId || (p.PublishedAt != null && !p.IsHidden))).Select(p => p.Id).ToArrayAsync(ct);
        if (linkedIds.Except(allowedLinks).Any() || blocks.Any(b => b.Kind == "article" && b.LinkedPostId is null && b.Id != addedPlaceholder))
            return new(422, "Link a published article or one of your own drafts.", note.Version);
        note.Title = command.Title.Trim();
        note.NotebookId = command.NotebookId;
        note.UpdatedAt = DateTimeOffset.UtcNow;
        note.Version = Guid.NewGuid();
        var keptIds = blocks.Select(b => b.Id).ToHashSet();
        db.StudyNoteBlocks.RemoveRange(existingBlocks.Where(b => !keptIds.Contains(b.Id)));
        for (var i = 0; i < blocks.Count; i++)
        {
            var source = blocks[i];
            var block = existingBlocks.SingleOrDefault(b => b.Id == source.Id);
            if (block is null) { block = new StudyNoteBlock { Id = source.Id, NoteId = id }; db.StudyNoteBlocks.Add(block); }
            block.Position = i;
            block.Kind = source.Kind;
            block.Content = source.Content ?? "";
            block.Language = source.Kind == "code" ? source.Language?.Trim() : null;
            block.LinkedPostId = source.Kind == "article" ? source.LinkedPostId : null;
        }
        var existingTags = await db.StudyNoteTags.Where(t => t.NoteId == id).ToListAsync(ct);
        db.StudyNoteTags.RemoveRange(existingTags.Where(t => !tags.Contains(t.Name)));
        db.StudyNoteTags.AddRange(tags.Except(existingTags.Select(t => t.Name)).Select(t => new StudyNoteTag { NoteId = id, Name = t }));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return new(409, Conflict, Guid.Empty); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { return new(422, "A block could not be saved. Reload the note and try again.", Guid.Empty); }
        await transaction.CommitAsync(ct);
        return new(200, null, note.Version);
    }

    public async Task<SaveResult> SetArchivedAsync(Guid id, Guid ownerId, Guid version, bool archived, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var note = await db.StudyNotes.FromSqlInterpolated($"SELECT * FROM \"StudyNotes\" WHERE \"Id\" = {id} AND \"OwnerId\" = {ownerId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (note is null) return new(404, "Note not found.", Guid.Empty);
        if (note.Version != version) return new(409, Conflict, note.Version);
        if (note.IsArchived == archived) return new(200, null, note.Version);
        note.IsArchived = archived;
        note.UpdatedAt = DateTimeOffset.UtcNow;
        note.Version = Guid.NewGuid();
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return new(409, Conflict, Guid.Empty); }
        await transaction.CommitAsync(ct);
        return new(200, null, note.Version);
    }
}
