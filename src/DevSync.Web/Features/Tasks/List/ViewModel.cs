using DevSync.Web.Features.Tasks.Common;
using DevSync.Web.Features.Tasks.Edit;

namespace DevSync.Web.Features.Tasks.List;

public sealed record ViewModel(IReadOnlyList<TaskItem> Tasks, IReadOnlyList<BoardChoice> Boards,
    int Page, int Pages, int Total, int Active, int Completed, int Archived, Guid? BoardId,
    string Search, string Priority, string State, string Assignment, string Due, string Sort);
