using DevSync.Web.Features.Tasks.Common;
using DevSync.Web.Features.Tasks.Edit;

namespace DevSync.Web.Features.Tasks.Board;

public sealed record Column(Guid Id, string Name, string Key, IReadOnlyList<TaskItem> Tasks);
public sealed record ViewModel(Guid? BoardId, string BoardName, Guid BoardVersion, IReadOnlyList<BoardChoice> Boards,
    IReadOnlyList<Column> Columns, string Search, string Priority, string State, string Assignment, string Due, string Sort, bool IsFiltered);
