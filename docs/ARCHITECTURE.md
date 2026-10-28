# Architecture decisions

Status: functional architecture through phase 8, 8 October 2026.

## Application organization

Use a modular monolith with MVC controllers and server-rendered Razor views. Use vertical slices for business actions; separate domain and persistence assemblies so domain rules remain independent of ASP.NET and EF Core. Web references Domain and Infrastructure; Infrastructure references Domain. Dependencies must not point back to Web.

The Home/Index slice establishes attribute routing, injected handlers, typed view models, and feature-folder view discovery. A write slice will add input validation, an authorization policy, database changes, and tests. No mediator or generic repository is required for this baseline.

## Persistence

Use PostgreSQL 17 in local Docker Compose, EF Core 10, and Npgsql 10. Pin package versions and commit dependency lock files. Track schema changes with migrations and apply them explicitly. Do not auto-migrate production on application startup.

The initial migration was generated and applied to local PostgreSQL. The test database is separate and must have a name ending in `_tests`.

Identity uses GUID keys. A DeveloperProfile has a unique handle, a unique user ID, and a foreign key to the Identity user. Profile handles are normalized before accepting user input. Time values use UTC DateTimeOffset. Mutable aggregates use concurrency tokens.

## Local Docker runtime

Compose starts PostgreSQL, then an explicit one-shot `--initialize-database` command that applies migrations and provisions roles, then the web service. HTTP startup does not migrate the database. `ConnectionStrings:DefaultConnection` takes precedence; otherwise Infrastructure builds the connection string from `Postgres` components using Npgsql's escaping. Docker uses the PostgreSQL service hostname and internal port rather than the host's published port.

The multi-stage image compiles Tailwind, builds the complete solution, runs HTTP/rendering and editor tests, and publishes the web application into a non-root .NET runtime image. Build context excludes local secrets, mail, key files, caches, and host build output. Compose mounts durable PostgreSQL data and web `.local` data (development mail and Identity keys), and gates web readiness on PostgreSQL connectivity and applied migrations. The provided stack uses Development mode and localhost HTTP; production deployment remains a separate phase-9 task.

## Authentication and authorization

Identity registration, confirmation, sign-in, sign-out, password recovery, and current-user profile editing are implemented as phase-3 slices. Member, Moderator, and Administrator policies are registered; publisher verification is a separate state, and team membership uses per-team roles. Ownership and team membership must be checked for each resource operation. Profile operations derive ownership from authenticated claims rather than posted IDs.

Handles are case-normalized and unique; normalized emails have a unique PostgreSQL index. Profile version tokens guard stale edits. Registration persists the user, Member assignment, and profile in one transaction. Role initialization uses PostgreSQL conflict handling to remain idempotent under concurrent registrations.

Email confirmation is required before sign-in. Development email defaults to owner-readable `.eml` files; explicit SMTP mode enables real delivery in Development, and production defaults to SMTP. Team invitations use the same adapter after their database transaction commits. See [account rules and acceptance checks](ACCOUNT-FLOWS.md). Account-mail workflows were verified using an in-memory test mailbox; the SMTP adapter requires live-provider delivery verification before deployment.

Unsafe MVC methods validate antiforgery tokens globally. Error pages preserve HTTP status codes and avoid displaying exception details. Readiness checks do not expose connection strings in HTTP responses.

## Frontend

Compile Tailwind CSS 4 with the CLI, explicitly scanning feature views, shared views, and JavaScript. The palette is provisional based on the visible Figma canvas; exact font, token, asset, and breakpoint inspection remains outstanding. Use Razor partials and View Components for repeated interface elements.

The verified baseline pins Tailwind and its CLI to 4.3.0, with compatible transitive dependency fixes captured in `package-lock.json`. The final npm audit reports no vulnerabilities.

Use progressive enhancement for autosave and task movement, while keeping authorization and validation on the server. Rich content must be sanitized before rendering. Uploaded content needs type/size restrictions and durable storage before it is enabled.

## Blog implementation

Blog actions live under `Features/Blogs`: Create, Edit (including versioned save/publish commands), MyBlogs, Explore, Read/Preview, and Interact. The MVC controller applies policy/antiforgery checks and dispatches to handlers. The Domain assembly owns the post/tag/bookmark/reaction/comment entities; Infrastructure owns their mappings and migration.

Each post stores a private working copy and a separate public snapshot. `PublishedAt` determines publication state. Publishing updates the snapshot and related public tag rows atomically; autosave never changes them. The first publication assigns a permanent title-stem/ID slug. GUID concurrency versions prevent conflicting writes. Composite bookmark/reaction keys and explicit-state upserts make retries idempotent.

The editor stores a small Markdown dialect. Its renderer HTML-encodes all content and permits absolute HTTP/HTTPS links only; HTTPS Markdown images are supported with encoded attributes, lazy loading, no-referrer requests, and responsive sizing; arbitrary HTML is never accepted for rendering. The sole `Html.Raw` boundary receives renderer output rather than stored user HTML. See [blog rules and acceptance checks](BLOG-FLOWS.md).

## Notes and revision implementation

Study notebooks, notes, typed ordered blocks, and tags are mapped in Infrastructure and served by `Features/Notes` List/Edit/Preview slices. Notes are personal and private. Composite owner foreign keys prevent assignment to another user's notebook; the note version protects the aggregate, including block order and archive state. Saving and archive/restore lock the owned note before updating child rows. Article links are limited to published snapshots or the owner's private working copy, and access is checked again when rendering. Text/code/checklists are Razor encoded.

Revision cards are independent prompt/answer snapshots linked to an owned note. Dashboard/Create/Review slices use an injected `TimeProvider` and the Domain scheduler. Reviews lock the source note before the card, atomically advance a versioned schedule and append history, and enforce unique submission/version keys. Archive suspends cards while retaining review history; restore resumes eligibility. Due times and daily review boundaries use UTC. See [notes and revision rules](NOTES-REVISION-FLOWS.md) for the document format, deterministic grading intervals, and retention behavior.

## Tasks implementation

Personal boards, columns, tasks, checklist items, and immutable activity records are mapped in Infrastructure and served through `Features/Tasks` List/Board/Edit/Details slices. Both presentations query the same task rows. A column's Done state drives task completion; archive/restore retains history and keeps active ordering contiguous. Assignment currently supports the owner or unassigned. Composite board/column ownership relations prevent cross-owner placement.

Mutations lock the owned board before task rows. Task versions protect editor and detail actions; moves additionally require the board version so stale positions cannot replace a newer ordering. Task state, column positions, versions, and activity save in one transaction. Date-only deadlines use PostgreSQL `date`; the injected clock supplies the UTC calendar day for filters and Home counters. Linked knowledge is validated on save and rendered through owner-note or public-snapshot/owner-draft access checks. See [task workflow rules](TASK-FLOWS.md).

## Teams implementation

Features/Teams contains List, Manage, Invitations, and Workspace slices. Infrastructure maps teams, composite-key memberships, invitation hashes, independently versioned workspace items, and activity records. Team copies are separate aggregates with no retained private source IDs; sharing does not widen personal-resource ownership checks. Personal revision schedules remain private. Internal articles use the existing safe Markdown renderer and do not publish automatically.

All mutations lock the team before checking current membership and role. Membership/invitation changes check the team version; content and archive actions check the item version. Revocation and role downgrades clear task assignments and advance affected item versions. Ownership transfer releases the filtered unique owner index and assigns the successor inside one transaction. Invitation acceptance locks the team and accepting account, checks the confirmed normalized email and token lifetime, and consumes the token in the membership transaction. Only token hashes are persisted. Read routes require current membership; platform roles do not bypass this boundary. See [team workflow rules](TEAM-FLOWS.md).

## Settings and governance implementation

Account preferences are a versioned account-owned aggregate. Public profiles join explicit opt-in preferences and expose only approved public fields and visible published snapshots. Reduced motion affects the authenticated layout; anonymous rendering does not resolve database-backed preferences.

Verification stores private evidence URLs and statements with a pending/approved/rejected/revoked state machine. Account row locks and filtered uniqueness serialize request/review transitions; reviewers cannot review themselves. Platform Moderator/Administrator policy protects queues and decisions independently of team roles.

Reports preserve bounded target excerpts and immutable target IDs. Moderation locks the article before report/comment rows, checks report and target versions, and changes visibility with immutable audit writes in one transaction. Article visibility is enforced across public discovery, reader actions, profiles, and linked knowledge. Audit actor deletion uses SET NULL to retain history. See [governance workflow rules](GOVERNANCE-FLOWS.md).

## Product assumptions pending confirmation

- Personal notes and tasks are private unless explicitly shared with a team.
- Blogs implement private drafts, explicit public snapshots, and versioned moderation visibility.
- Start collaboration with ordinary HTTP requests; add real-time updates only after behavior is defined.
- Publisher verification grants a badge initially; the initial publishing baseline for phase 4 allows confirmed Members to draft and publish.
- Account policy is established: verified email before sign-in, two-hour verification/recovery links, generic recovery acknowledgement, and local mail delivery for development.
- Blog Markdown and typed note blocks are defined. Notes/revision use an archive/restore retention policy; irreversible deletion is not provided. Revision interval policy is documented and testable; personalized scheduling can follow a future product decision.

These defaults guide the implemented functional baseline; exact visual parity and production readiness remain in phase 9.
