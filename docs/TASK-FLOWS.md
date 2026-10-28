# Tasks and Kanban workflow

## Personal workspace

`/tasks` and `/tasks/board` read the same private task data. Each personal board has To do, In progress, and Done columns. Create a board, then add a task with a title, description, priority, optional due date, checklist, and optional note/article links. Tasks can be assigned to you or left unassigned. Shared content and assignments live in separate private [team workspaces](TEAM-FLOWS.md); personal boards retain owner-only access.

The board stores the order within each column. Moving into Done completes a task; moving out reopens it. Complete and Reopen use the same rules. Archiving keeps the task and its activity but removes it from the active board; restoring appends it to its column. Archived tasks have a separate filter and must be restored before editing or moving.

Limits: 20 personal boards per user, 200 active tasks per board, 30 checklist items per task. Board names use up to 80 characters, titles 160, descriptions 4,000, and checklist labels 300. List results are paginated in groups of 20, while the board shows the bounded active task set. Without JavaScript, adding a checklist item saves the task and creates an empty item to fill next; remove can discard that placeholder.

## Movement and conflicts

Keyboard users can select a destination column and use earlier/later controls. Dragging is an enhancement over the same authenticated, antiforgery-protected server operation. Reordering is available in the complete board-order view; filters and alternative sorting can hide intervening tasks, so the interface restricts ordering there. Moving to another column remains available.

Every mutation locks the owned board before changing task rows. Task versions protect edits, checklist changes, completion, and archive state; moves also check the board version. Successful mutations advance versions and append activity atomically. A stale submission returns HTTP 409 with a visible message. Reload to review the current data before retrying; an old editor cannot silently overwrite the winning save.

## Linked knowledge and privacy

Task routes derive ownership from the signed-in identity. Another user's IDs return 404 and privileged application roles do not bypass personal ownership. Posted owner IDs cannot transfer tasks or boards. Assignment is restricted to the owner or unassigned.

Link choices include your active notes, your article drafts, and other authors' published articles. Public articles use their published title and URL, keeping later private draft edits hidden. Link access is checked again on save and display. An unavailable selected link gets a generic label without revealing its title; choose another link or explicitly remove it before saving. Task descriptions, checklist labels, and activity are rendered as encoded text.

## Due dates

Deadlines are calendar dates stored as PostgreSQL `date`, without local-time conversion. Today is determined from the injected clock's UTC date. An open task is overdue when its date is before today; today and upcoming filters use equality and dates after today. Completed and archived tasks are excluded from Home's open/overdue counters. Empty dates represent no deadline.

## Verification

The PostgreSQL suite exercises the same HTTP routes as the browser, verifies reload consistency, owner isolation, permitted links, assignment validation, checklist persistence, movement conflicts, contiguous column positions, and UTC-date filters. Successful redirect targets are requested explicitly so route or Razor failures cannot be masked by a 302 response. Dedicated test users and their task data are cleaned from `devsync_tests` after the suite.

Docker rebuilds compile Tailwind and the full solution, run the non-database and client suites, and publish the runtime. Compose applies migrations through the one-shot initializer before starting the web service. This local workflow does not complete the release phase or exact Figma visual review.

The phase-6 verification run passed 36 non-database tests, 32 PostgreSQL tests, and 27 client tests. Live browser checks verified native column moves, keyboard reordering, checklist persistence, dates, linking, completion, archive/restore, and consistent list/board counts. At 390 px, the page stays within the viewport while the board scrolls internally. Pointer-drag browser verification was inconclusive; manual pointer and cross-browser review remains a release task even though the drag/drop client tests pass.
