using DevSync.Web.Features.Tasks.Common;
using DevSync.Web.Features.Tasks.Edit;

namespace DevSync.Web.Features.Tasks.Details;

public sealed record ViewModel(Guid Id, Guid Version, Guid BoardId, string BoardName, Guid BoardVersion,
    Guid ColumnId, string ColumnName, string ColumnKey, int Position, string Title, string Description,
    string Priority, DateOnly? DueDate, bool AssignedToMe, bool IsArchived, DateTimeOffset? CompletedAt,
    DateTimeOffset UpdatedAt, IReadOnlyList<ColumnChoice> Columns, IReadOnlyList<ChecklistItem> Checklist,
    LinkChoice? Note, LinkChoice? Article, IReadOnlyList<ActivityItem> Activities, int ActivityPage, int ActivityPages, int ActivityTotal);
