using DevSync.Domain.Revision;
using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevSync.Web.Features.Revision.Create;

public sealed class Handler(AppDbContext db, TimeProvider clock)
{
    public Task<Command?> LoadAsync(Guid noteId, Guid ownerId, CancellationToken ct) =>
        db.StudyNotes.AsNoTracking().Where(n => n.Id == noteId && n.OwnerId == ownerId && !n.IsArchived)
            .Select(n => new Command { NoteId = n.Id, NoteTitle = n.Title }).SingleOrDefaultAsync(ct);

    public async Task<Guid?> HandleAsync(Guid ownerId, Command command, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // A note archive and card creation cannot race past each other.
        var notes = await db.StudyNotes.FromSqlInterpolated($"SELECT * FROM \"StudyNotes\" WHERE \"Id\" = {command.NoteId} AND \"OwnerId\" = {ownerId} FOR UPDATE").ToListAsync(ct);
        var note = notes.SingleOrDefault();
        if (note is null || note.IsArchived) return null;
        var now = clock.GetUtcNow().ToUniversalTime();
        var card = new RevisionCard { OwnerId = ownerId, NoteId = note.Id, Prompt = command.Prompt.Trim(),
            Answer = command.Answer.Trim(), DueAt = now, CreatedAt = now };
        db.RevisionCards.Add(card);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return card.Id;
    }
}
