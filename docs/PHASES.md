# DevSync phased task backlog

Started: 7 October 2026. Statuses: **done**, **in progress**, **ready**, **planned**. Phase completion requires implementation and recorded verification; an existing folder is not completion.

## Phase overview

| Phase | Scope | Depends on | Current status |
|---|---|---|---|
| 1 | Scope, screen map, architecture, acceptance | — | Baseline done; detailed design review pending |
| 2 | Solution, PostgreSQL, Tailwind, first MVC slice | 1 baseline | Done |
| 3 | Account, authorization, profile basics, shared UI | 2 | Functional milestone done; exact design parity pending |
| 4 | Complete blog author-to-reader workflow | 3 baseline | Functional milestone done; exact design parity pending |
| 5 | Study notes and spaced repetition | 3, 4 content linking | Functional milestone done; exact design parity pending |
| 6 | Tasks, Kanban, linked knowledge | 4, 5 | Functional milestone done; manual pointer and exact design review pending |
| 7 | Teams and shared workspaces | 3–6 | Functional milestone done; exact design parity pending |
| 8 | Settings, public profiles, verification, moderation | 3–7 | Functional milestone done; exact design parity pending |
| 9 | Cross-feature validation and release preparation | 3–8 | In progress — local operational milestone verified |

## Phase 1 — Product and design baseline

- [x] P1-01 Record all named Figma screens and map them to feature slices.
- [x] P1-02 Define MVC + vertical slice organization and dependency direction.
- [x] P1-03 Document initial data model, visibility defaults, roles, and unresolved product rules.
- [x] P1-04 Divide the full scope into phases and implementation tasks.
- [ ] P1-05 Inspect exact design tokens, assets, responsive variants, and prototype transitions.
- [ ] P1-06 Define detailed acceptance criteria and missing states per screen before its implementation phase.

Acceptance: an agreed implementation baseline and traceable screen inventory. Exact visual verification remains a task; the foundation can proceed from the observed design.

## Phase 2 — Working application foundation (completed milestone)

- [x] P2-01 Create .NET solution with Web, Domain, Infrastructure, and Tests projects; verify dependencies and build.
- [x] P2-02 Configure PostgreSQL Compose service, local secrets workflow, EF Core, and Npgsql.
- [x] P2-03 Add Identity/profile persistence and generate/apply the initial migration.
- [x] P2-04 Configure MVC feature-folder views and implement the Home/Index slice.
- [x] P2-05 Add Tailwind build/watch/publish commands and a responsive shell.
- [x] P2-06 Add safe error pages and separate liveness/readiness checks.
- [x] P2-07 Verify HTTP behavior, real PostgreSQL migrations/constraints, and release output.
- [x] P2-08 Document reproducible local startup and verify the UI in a browser.

Acceptance: the app starts from the documented commands, `/` renders with compiled styles, PostgreSQL migrations apply, readiness is healthy, tests pass, and release output includes assets. This phase establishes the shell; it does not mark account or business modules complete.

## Phase 3 — Account and shared interface

- [x] P3-01 Define registration, confirmation, recovery, and publishing eligibility rules.
- [x] P3-02 Implement register/sign-in/sign-out slices, validation, local return URLs, and lockout behavior.
- [x] P3-03 Implement email confirmation/password recovery with a development delivery mechanism and production adapter.
- [x] P3-04 Provision application roles idempotently; add authorization policies.
- [x] P3-05 Implement profile creation/editing and case-normalized unique handles.
- [ ] P3-06 Extend navigation, account menu, forms, alerts, and reusable components to the Figma specification.
- [x] P3-07 Verify account persistence, antiforgery, access-denied behavior, and identity/resource authorization.

Acceptance: a user can register, authenticate, manage basic identity, and sign out; protected operations enforce policies, with tested error/validation states.

See [account workflow rules](ACCOUNT-FLOWS.md). The publishing baseline for phase 4 is that a confirmed Member can draft and publish; publisher verification remains a separate badge/workflow in phase 8. Account layouts follow the observed palette; exact Figma component/font/token parity remains under P1-05/P1-06.

P3-06 implementation includes the account menu, navigation, form styles, validation summaries, success notices, and dedicated account layout. It remains unchecked until exact Figma parity is verified; this does not block implementation of the next functional milestone.

## Phase 4 — Technical blogs

- [x] P4-01 Add posts, tags, draft/published states, author relations, indexes, and migration.
- [x] P4-02 Implement Create, Edit, SaveDraft, Preview, and Publish slices.
- [x] P4-03 Define and integrate an accessible editor with safe code/content rendering and conflict-aware autosave.
- [x] P4-04 Implement MyBlogs, Explore, Read, pagination, filtering, and stable slugs.
- [x] P4-05 Add bookmarks, reactions, comments, and ownership checks as specified in the reader.
- [x] P4-06 Connect the Home hub to real published content and current-user data.
- [x] P4-07 Verify draft privacy, publish transitions, untrusted content, authorization, and reader layouts.

Acceptance: an author can create/edit/publish an article; readers can find/read it; drafts remain private and social actions persist without duplicates.

See [blog workflow rules and acceptance checks](BLOG-FLOWS.md). The editor uses safe Markdown with manual saving plus conflict-aware autosave; published articles keep an independent snapshot and permanent URL. Populated MVC test responses are available for read-only layout review without adding demo users/content to the development database. Exact Figma component/font/token parity remains under P1-05/P1-06 and P3-06.

## Phase 5 — Study notes and revision

Implementation baseline: personal notebooks and private notes with ordered paragraph, heading, code, checklist, and article-link blocks. Revision cards store a question/answer linked to a note. Scheduling uses documented graded intervals with UTC due times; archiving a note suspends its review cards. Team sharing follows in phase 7.

- [x] P5-01 Add notebooks, notes, ordered blocks, tags, private ownership, and migrations.
- [x] P5-02 Implement List, Create, Edit, MoveBlock, Archive, and Search slices.
- [x] P5-03 Integrate the block editor, safe code blocks, conflict-aware autosave, and article links.
- [x] P5-04 Decide revision scheduling algorithm and grading behavior.
- [x] P5-05 Implement revision items, due queries, review submission, scheduling, and history.
- [x] P5-06 Build revision dashboard with real counts and accessible empty states.
- [x] P5-07 Verify privacy, block ordering, scheduling boundaries, repeated submissions, and UTC handling.

Acceptance: notes persist reliably; review grades update the next due time deterministically; only permitted users can access notes and revision history.

See [notes and revision workflow rules](NOTES-REVISION-FLOWS.md). Personal visibility is enforced through ownership and composite owner foreign keys. Team sharing remains a phase-7 task. Exact Figma assets/components/token parity remains under P1-05/P1-06, P3-06, and P9-02.

## Phase 6 — Tasks and Kanban

Implementation baseline: private personal boards with To do, In progress, and Done columns. List and Kanban share tasks, priorities, date-only deadlines, checklists, and linked knowledge. Assignment supports the owner or unassigned; team membership and team assignments follow in phase 7. Versioned server actions and keyboard controls support movement without JavaScript, with drag-and-drop as progressive enhancement.

- [x] P6-01 Add boards, columns, tasks, ordering, priority, due dates, checklists, and activity records.
- [x] P6-02 Implement Create, Edit, Details, Complete, Archive, and checklist slices.
- [x] P6-03 Implement List and Kanban views against the same model; add filtering/sorting.
- [x] P6-04 Implement server-validated move/reorder operations with concurrency handling and keyboard alternatives.
- [x] P6-05 Link tasks to notes/blogs with access checks on linked resources.
- [x] P6-06 Add assignments and activity display; enforce valid membership/ownership.
- [x] P6-07 Verify persistence after reload, move conflicts, visibility, checklist updates, and due-date behavior.

Acceptance: both views stay consistent, task moves persist, linked private content remains protected, and drag-and-drop has an accessible alternative.

See [task workflow rules](TASK-FLOWS.md). Native column moves and keyboard reordering were verified in the live browser, with persistence and concurrency covered by PostgreSQL tests. Drag/drop client behavior passes automated tests; manual pointer and cross-browser review remains under P9-03. Exact Figma parity remains under P1-05/P1-06 and P9-02.

## Phase 7 — Teams and collaboration

Implementation baseline: private team workspaces with Owner/Admin/Member/Viewer roles and email-bound, expiring invitation links. Explicit sharing creates independent editable copies of personal notes, article drafts, and tasks; personal originals and revision schedules retain owner-only access. Team task assignments require a current writer. Manual HTTP saving and version conflicts provide collaboration without real-time subscriptions. See [team workflow rules](TEAM-FLOWS.md).

- [x] P7-01 Add teams, memberships, team roles, invitations, and shared workspace relations.
- [x] P7-02 Implement CreateTeam, Invite, AcceptInvite, ChangeRole, Leave, and RemoveMember slices.
- [x] P7-03 Add team dashboard and workspace views for shared blogs, notes, and tasks.
- [x] P7-04 Enforce team authorization across all existing read and write handlers.
- [x] P7-05 Define conflict behavior; add real-time updates only if required by agreed workflows.
- [x] P7-06 Verify invite expiration/reuse, cross-team isolation, ownership transfer, and access revocation.

Acceptance: members collaborate within permitted teams; outsiders cannot access resources; revoking membership removes access across team features. Existing personal handlers retain owner-only checks; team routes operate on independent copies. Exact Figma and full release accessibility review remain under phase 9.

## Phase 8 — Profiles, settings, publishing, moderation

- [x] P8-01 Build public profiles backed by real published content and safe profile fields.
- [x] P8-02 Implement account preferences and settings shown in the design.
- [x] P8-03 Define verification evidence/storage rules; implement request/review/approve/reject workflow.
- [x] P8-04 Add report submission, moderation queue, hide/restore actions, and audit records.
- [x] P8-05 Restrict administration and moderation actions by policy, with explicit action validation.
- [x] P8-06 Verify visibility after moderation, verification transitions, audit history, and role boundaries.

Acceptance: settings persist, verification has a traceable state machine, and authorized moderators can act with an audit trail.

See [profiles, settings, verification, and moderation rules](GOVERNANCE-FLOWS.md). Public profiles require opt-in; verification is a badge independent of publishing permissions. Moderation enforces platform roles and versioned decisions. Settings implement the functional baseline; exact Figma parity remains under P1-05/P1-06 and P9-02.

## Phase 9 — Release preparation

See [release configuration, CI, recovery, monitoring, and remaining gates](RELEASE-RUNBOOK.md). Production hosting is not configured. Exact Figma inspection and full accessibility/cross-browser verification remain pending.

- [x] P9-01 Run complete cross-feature workflows and permission regression tests.
- [ ] P9-02 Compare implemented screens with Figma at agreed desktop/mobile sizes.
- [ ] P9-03 Verify keyboard navigation, labels, contrast, responsive layout, reduced motion, and pointer drag/drop across browsers.
- [ ] P9-04 Measure representative queries; add indexes, pagination, and caching where justified.
- [ ] P9-05 Complete upload restrictions, content sanitization, throttling, and dependency checks.
- [ ] P9-06 Configure release builds/CI, production secrets, durable key storage, logs, and health probes.
- [x] P9-07 Document migration, backup/restore, monitoring, and rollback procedures.
- [ ] P9-08 Prepare a release candidate; choose hosting and perform deployment when authorized.

Acceptance: product workflows and access boundaries pass, visual/accessibility review is complete, and deployment/rollback procedures are tested.

## Verification log

### 7 October 2026 — Foundation milestone

- Dependency restore succeeded; .NET package lock files and `package-lock.json` are included.
- `dotnet build DevSync.slnx --no-restore --disable-build-servers -p:UseSharedCompilation=false`: passed with zero warnings and zero errors.
- `docker compose up -d --wait postgres`: container healthy on localhost port 54329.
- Generated and applied `InitialIdentityAndProfiles`; Identity tables, profile foreign key, and unique indexes are present.
- `dotnet test DevSync.slnx --filter 'Category!=Database' --no-restore`: 4 passed, 0 failed, 0 skipped.
- `npm run db:test -- --no-build --no-restore`: 1 PostgreSQL integration test passed against `devsync_tests`, with transactional rollback.
- `npm run css:build`: passed using Tailwind 4.3.0. Compatible transitive dependency fixes applied; npm audit reports 0 vulnerabilities.
- `npm run publish -- --no-restore`: succeeded; published output includes the Web assembly, CSS, JavaScript, and favicon.
- Local HTTP `/health/ready`: Healthy after migration.
- Browser checked the home page at the default narrow width and at 1440 px, including opening and closing mobile navigation. Screenshot saved to `artifacts/devsync-home-desktop.png` (local ignored artifact).
- Local preview left running at `http://localhost:5180` for review. PostgreSQL remains running with its persistent local volume.

The execution sandbox stalled some native .NET/Tailwind operations and blocked Docker/local HTTP access. These checks completed successfully with tool-level permission outside the sandbox. No business feature beyond Home/Index is marked implemented; account workflows begin in phase 3.

### 7 October 2026 — Account functional milestone

- Implemented Register, SignIn, ConfirmEmail, RequestEmail, ResetPassword, and current-user Profile/Edit slices, plus POST-only sign-out.
- Generated and applied `AccountIntegrityAndProfileVersion`: normalized email uniqueness, lowercase handle constraint, and profile concurrency version.
- Provisioned Member, Moderator, and Administrator roles idempotently in the local database. Registration grants Member only; no privileged account was created.
- HTTP suite: **10 passed**, 0 failed, 0 skipped. Covers existing home/health/error behavior, public account forms, antiforgery rejection, and request limiting.
- PostgreSQL suite: **7 passed**, 0 failed, 0 skipped. Covers registration/verification/sign-in/sign-out, profile editing and conflicts, recovery-token replay and session revocation, duplicate identifiers, invalid password rollback, lockout, role restrictions, and persistence constraints.
- An initial build overlap caused a generated-file lock; verification was rerun sequentially. One test's relative redirect assertion was corrected to check ASP.NET's absolute sign-in URL and localhost host.
- All account test mail was captured in memory. Fixture-created users were removed from the dedicated `_tests` database; no external mail was sent.
- Local readiness endpoint reports **Healthy** with both migrations applied.
- Sign-in reviewed at the default narrow width and 1440 px; registration reviewed at narrow width. Desktop sign-in screenshot saved to `artifacts/devsync-sign-in-desktop.png`.
- Updated preview is running at `http://localhost:5180/account/sign-in`.
- Release publish succeeded; the Web assembly and compiled stylesheet are present, with no `.local` directory or `.eml` files in the output. Development mail/key files are ignored by Git and explicitly excluded from Web project publish items.

### 7 October 2026 — Blog functional milestone

- Implemented author dashboard, creation, editor/manual saving, conflict-aware autosave, private saved-draft preview, publishing, public reader, explore/search/tag filtering, and pagination.
- Added `BlogPublishingAndReaderActions` and applied it to local development/test PostgreSQL. Public content/tag snapshots remain independent from private working edits; republishing preserves the original URL.
- Implemented idempotent likes/bookmarks, saved-article listing, plain-text comments with pagination/writer deletion, and Home feeds/current-author counts.
- Final HTTP/rendering suite: **15 passed**, 0 failed, 0 skipped. Final PostgreSQL suite: **12 passed**, 0 failed, 0 skipped. Editor client suite: **3 passed**, 0 failed. Total: **30**.
- Checks include draft privacy, ownership, posted-ID rejection, publication validation, public snapshot isolation, stale and simultaneous writes, stable slugs, safe HTML/code/link rendering, discovery, comment ownership, and reader-action retries.
- Corrected a test fixture that gave pagination articles future timestamps and displaced the newly published article from Home's latest-three feed. Regenerated development assemblies after incremental Razor output retained an earlier navigation layout; final checks use the rebuilt source.
- Reviewed populated MVC test snapshots for reader/editor/dashboard at 390 px; mobile navigation worked, reader/editor had no horizontal overflow. Public explore reviewed at 1280 px with current active navigation. Exact Figma tokens/assets/parity remain pending.
- Release publish succeeded with current compiled CSS and editor JavaScript. Output contains no development mail or key files. Local readiness reports **Healthy** with all three migrations applied.
- Integration fixtures removed their users/articles from the dedicated test database; cleanup query returned zero test users and zero posts. Test mail remained in memory; no external email was sent.
- Live local preview: `http://localhost:5180/blogs`. Screenshot: `artifacts/devsync-blogs-desktop.png`. Optional read-only review snapshots/scripts are documented in [blog workflow rules](BLOG-FLOWS.md).
- Next functional milestone: phase 5, personal study notes and spaced repetition.

### 7 October 2026 — Docker rebuild and demo-login repair

- Added a multi-stage Dockerfile that builds Tailwind and the complete .NET solution, runs HTTP/rendering and editor tests, and publishes a non-root runtime image. `.dockerignore` excludes secrets, local mail/keys, caches, and native build output.
- Added Compose web and one-shot database initializer services. PostgreSQL health and successful migration/role initialization gate web startup; the initializer exited with code 0 and both persistent services reported healthy.
- Added safely escaped PostgreSQL component configuration for container networking, while retaining explicit connection-string precedence. Configuration tests cover special-character passwords, service host/port, precedence, and incomplete configuration.
- Docker solution build: **0 warnings, 0 errors**. HTTP/configuration/rendering tests: **17 passed**. Editor client tests: **3 passed**. PostgreSQL tests from the Docker SDK image against `devsync_tests`: **12 passed**. Total: **32**, no failures or skipped tests.
- Diagnosed the reported demo sign-in error: the proposed account had not been created. Created `demo@devsync-test.invalid` through registration, verified its local development email, and verified Member sign-in/dashboard access through both HTTP and the browser. Credentials are recorded in the README for this local database; the initializer does not seed accounts into new databases.
- Restarted the Docker web service and reloaded the authenticated dashboard: the session remained valid, verifying durable Identity protection keys. Database and web-local data remain in named volumes.
- Local application now runs in Docker at `http://localhost:5180`. Browser proof: `artifacts/devsync-demo-signed-in.png`. The README documents Docker startup, local verification mail, demo sign-in, and refreshing old forms after switching runtimes.
- This local runtime milestone does not complete phase 9's production deployment or the remaining product phases.

### 7 October 2026 — Notes and revision functional milestone

- Implemented personal notebooks, private notes, tags, title/block search, filtering, pagination, archive/restore, and ordered paragraph/heading/code/checklist/article-link blocks. Linked articles enforce public-snapshot or owner-draft access on save and preview.
- Added a progressive editor with manual saving, keyboard block controls, safe saved preview, and autosave that preserves edits made during a save. Server block actions support JavaScript-free add/move/remove. Whole-note versions and aggregate row locks prevent stale or simultaneous saves from mixing content/order.
- Implemented revision-card creation, due/scheduled queues, answer reveal, four grade choices, deterministic UTC intervals, and private paginated history. Schedule/history writes are atomic; duplicate submission IDs replay safely and distinct stale versions cannot advance the schedule twice. Archive suspends cards and keeps their history.
- Generated and applied `20261007095406_StudyNotesAndRevision`, adding six tables and ownership/scheduling constraints. All four development migrations are applied; Compose initializer exited with code 0 and web/PostgreSQL report healthy.
- Final Docker solution build/publish: **0 warnings, 0 errors**. HTTP/configuration/rendering/scheduler suite: **36 passed**. PostgreSQL suite: **24 passed**. Editor client suite: **15 passed**. Total: **75**, no failures or skipped tests.
- Browser verification caught an action-name ambiguity that redirected card creation/grading to the POST route. Both redirects now explicitly target the GET card page; regression assertions follow creation and grading redirects and verify rendered HTTP 200 responses.
- Live demo workflow verified notebook creation, three typed blocks, reordering and saving, persisted preview, revision-card creation, answer reveal, Good grading, scheduled next date, and history. The demo account retains one private example note/card for exploration; no public content or external email was created.
- Reviewed the editor/preview at 390 px and notes/revision at 1440 px. Document width matched viewport width, mobile navigation exposed both features, and temporary viewport settings were reset. Screenshots: `artifacts/devsync-notes-desktop.png`, `artifacts/devsync-note-editor-mobile.png`, `artifacts/devsync-note-mobile.png`, and `artifacts/devsync-revision-desktop.png`.
- Test fixtures cleaned their data: dedicated test database has zero users, notes, and cards. Existing development accounts/blogs were preserved, and the demo remained signed in through Docker replacements.
- Local application: `http://localhost:5180/notes` and `/revision`. Next functional milestone: phase 6, tasks and Kanban. Exact Figma visual parity remains pending.

### 7 October 2026 — Tasks and Kanban functional milestone

- Implemented private personal boards, three fixed columns, tasks, four priorities, calendar due dates, owner/unassigned assignment, ordered checklists, note/article links, archive/restore, and activity history. The list and Kanban use the same data with search, filters, sorting, and list pagination.
- Task mutations lock the owned board and check task versions; moves also check the board version. Stale and simultaneous writes cannot overwrite the winning task/checklist state or mix column order. Native column selection and earlier/later controls provide keyboard alternatives to the progressive drag/drop enhancement.
- Generated and applied `20261007141410_TasksAndKanban` and `20261007142256_TaskAssigneeRetention`. All six migrations are applied in development; PostgreSQL and web are healthy, and the Compose initializer exited with code 0.
- Final Docker solution build/publish: **0 warnings, 0 errors**. HTTP/configuration/rendering/scheduler suite: **36 passed**. PostgreSQL suite: **32 passed**. Client suite: **27 passed**. Total: **95**, no failures or skipped tests.
- Checks cover complete/reopen/archive/restore, owner and role isolation, CSRF, link visibility, forged assignment, checklist limits and JavaScript-free controls, contiguous ordering, simultaneous writes, board/task capacity, pagination, validation, and UTC-day boundaries.
- Fixed fixture cleanup by making the optional assignee foreign key clear on user deletion; the task's owner cascade still removes owned data. The final PostgreSQL suite completed successfully, and dedicated test cleanup returned zero users, tasks, boards, and notes. Development accounts and existing content were preserved.
- Live demo verification created the private Developer roadmap board and three example tasks. Checklist additions, reordering and toggles, due dates, assignment, a private note link, native column moves, completion, archive/restore, filters, and reload persistence worked. List and board agreed on two open tasks and one completed task.
- Reviewed list/board at 1440 px and editor/details/board at 390 px. Mobile document width matched the viewport; the wider board scrolls inside its container. Temporary viewport settings were reset. Screenshots: `artifacts/devsync-board-desktop.png`, `artifacts/devsync-tasks-desktop.png`, `artifacts/devsync-board-mobile.png`, `artifacts/devsync-task-mobile.png`, and `artifacts/devsync-task-editor-mobile.png`.
- Pointer-drag browser verification was inconclusive. Drag/drop payload, placement, submission, filtering, and duplicate-submit behavior pass client tests; manual pointer and cross-browser review remains under P9-03. Exact Figma assets/tokens/parity remain pending.
- Local application: `http://localhost:5180/tasks` and `/tasks/board`. Next functional milestone: phase 7, teams, invitations, and shared workspaces.

### 8 October 2026 — Teams and collaboration functional milestone

- Implemented private teams, Owner/Admin/Member/Viewer memberships, creation, member management, ownership transfer, leave/removal, invitation creation/revocation/acceptance, and shared workspace pages.
- Invitations use 256-bit random tokens with SHA-256 database hashes, seven-day expiry, confirmed normalized-email binding, replacement of previous unused links, and single-use acceptance. Links are shown once for manual delivery; no external invitation email was sent.
- Team notes, internal article drafts, and tasks are independently editable copies. Explicit sharing validates personal source ownership and omits private links; team membership never grants access to personal originals or revision schedules. Team tasks support status, priority, date-only deadlines, and current-writer assignment. Archive/restore retains content and activity.
- All writes lock the team before checking current membership and role. Item/team versions reject stale content and management forms. Ownership transfer keeps one owner; revocation and Viewer downgrades clear assignments and advance affected item versions. Collaboration uses manual HTTP saves and reloads; real-time subscriptions are not required for this milestone.
- Generated and applied 20261007181549_TeamsAndSharedWorkspace, adding five tables and role/content constraints. All seven development migrations are applied; PostgreSQL/web report healthy and the Compose initializer exited with code 0.
- Final Docker solution build/publish: **0 warnings, 0 errors**. HTTP/configuration/rendering/scheduler suite: **36 passed**. PostgreSQL suite: **38 passed**, including six team scenarios. Client suite: **27 passed**. Total: **101**, no failures or skipped tests.
- PostgreSQL HTTP checks cover email binding, expiration, replacement, revocation, replay and simultaneous acceptance; roles and platform-role isolation; ownership transfer and leave/removal; private-copy isolation, safe rendering, assignments, archive/restore, stale/concurrent saves, CSRF, validation, pagination, and workspace/account limits. Successful redirects are followed through rendered GET pages.
- Creation validation/capacity errors retain draft content, and stale saves keep the rejected version with saving paused. Final test cleanup returned zero users, teams, workspace items, and invitations in the dedicated test database. Existing development accounts and personal content were preserved.
- Live demo verification created DevSync builders with a note, internal article, and assigned task. Status, priority, due date, saving/reloading, archive/restore, validation draft retention, invitation creation and revocation worked. The temporary test invitation was revoked; the team retains only its original demo owner, with no external publication or messages.
- Reviewed the workspace at 1440 px and workspace/task editor/member pages at 390 px. All mobile document widths matched the viewport, browser error logs were empty, and temporary viewport settings were reset. Screenshots: artifacts/devsync-team-workspace-desktop.png, artifacts/devsync-team-workspace-mobile.png, artifacts/devsync-team-task-mobile.png, and artifacts/devsync-team-members-mobile.png.
- Local application: http://localhost:5180/teams. Next functional milestone: phase 8, public profiles, settings, publishing verification, and moderation. Exact Figma parity and comprehensive release review remain pending.


### 8 October 2026 — Profiles and governance functional milestone

- Implemented opt-in public profiles, independently controlled bio visibility, verified badges, visible published article pagination, reduced animation, and signed-in Explore/Saved page-size preferences. Settings use claim-derived ownership and concurrency versions; anonymous shell rendering remains database-independent.
- Implemented private HTTPS-link verification requests and pending/approved/rejected/revoked transitions with required reviewer reasons, self-review denial, one-active-request constraints, concurrency checks, and atomic audit records. Verification does not restrict ordinary Member publishing.
- Implemented article/comment reports, duplicate pending-report protection, platform-role moderation queues, hide/restore/dismiss actions, report and target concurrency checks, and retained excerpts for deleted comments. Hidden content is excluded from public readers/discovery/profiles and public linked knowledge; owner private edits remain available, and republishing hidden articles is rejected.
- Generated and applied 20261007190717_ProfilesSettingsAndGovernance. All eight development migrations are applied; Docker PostgreSQL/web are healthy, initialization exited successfully, and readiness reports Healthy.
- Docker build/publish: **0 warnings, 0 errors**. HTTP/configuration/rendering/scheduler suite: **36 passed**. PostgreSQL suite: **43 passed**, including five governance scenarios. Client suite: **27 passed**. Total: **106**, no failures or skipped tests.
- Tests cover privacy, transitions, evidence validation, simultaneous reviewer decisions, stale forms/targets, CSRF, platform role boundaries, audits, hiding/restoring linked content, and report retention. Test cleanup returned zero users, reports, verification requests, and governance audits in the dedicated test database.
- Live demo session survived Docker replacement. Read-only settings/verification review at 1440 px and 390 px showed no horizontal overflow and no browser errors; navigation exposed Settings and omitted privileged Moderation for the Member account. Preferences and existing personal/team data were preserved. Privileged workflows were verified through isolated PostgreSQL-backed HTTP tests.
- Screenshots: artifacts/devsync-settings-desktop.png, artifacts/devsync-settings-mobile.png, artifacts/devsync-verification-desktop.png, and artifacts/devsync-verification-mobile.png. Temporary viewport overrides were reset; Settings is left open for review.
- Local pages: http://localhost:5180/account/settings and http://localhost:5180/account/verification. Next milestone: phase 9, cross-feature validation and release preparation. Exact Figma parity, manual pointer/cross-browser review, and production deployment remain pending.


### 8 October 2026 — Release operations milestone (phase 9 in progress)

- Added a GitHub verification workflow for locked restore, Tailwind/client tests, Release build, all HTTP/PostgreSQL tests, and publish output, with a PostgreSQL 17 test service and read-only repository permissions. Workflow commands were exercised locally; a hosted GitHub run awaits a pushed repository.
- Added production startup validation for durable absolute Identity key storage, explicit allowed hosts, a matching HTTPS public origin, and SMTP host/sender. Production uses a stable key application name, Secure Identity/antiforgery cookies, HTTPS redirects, and explicitly trusted proxy IPs. Internal health probes remain available over backend HTTP.
- Production-host tests exposed a missing Secure antiforgery cookie flag; the production configuration now sets it. Validation resolves final host configuration before serving requests, allowing both environment configuration and isolated test-host overrides.
- Final Docker build/publish: zero warnings/errors. HTTP/configuration/rendering/scheduler/production-host tests: **47 passed**. PostgreSQL cross-feature suite: **43 passed**. Client tests: **27 passed**. Total: **117**, no failures or skipped tests. Production tests cover invalid settings, secure forms/HSTS, key reuse across hosts, untrusted forwarded scheme rejection, and internal health probes.
- npm and direct/transitive NuGet advisory scans reported no known vulnerabilities on this date. Existing renderer, private-resource, version/concurrency, role, and antiforgery regressions remain passing.
- Added owner-readable custom PostgreSQL backup and fresh-database restore scripts. Rehearsed restore from artifacts/backups/devsync-phase9.dump into devsync_phase9_restore_tests: two users, three articles, one team, eight migrations, 113 validated constraints, zero unvalidated constraints. Original development data was preserved; the isolated recovery copy is retained for inspection. Backup file mode is 0600.
- Documented required production settings, migration/rollout, durable key backup, monitoring, and rollback procedures in RELEASE-RUNBOOK.md. Production provider configuration and rollback rehearsal remain pending.
- Rebuilt local Docker runtime is healthy; existing demo sign-in survived. Settings controls retain their labels, keyboard Tab reaches Skip to content, activating it focuses main content, Enter opens mobile navigation, and Escape closes it and restores toggle focus. Browser error logs are empty. Screenshot: artifacts/devsync-release-smoke.png.
- Added cross-feature unsafe-request throttling: 120 writes per minute per account/anonymous IP, with configurable budgets, independent of reads and health probes. Status-code re-execution is exempt so validation errors consume one allowance and preserve their original status. The regression checks excess writes return 429 while reads stay available. Limits are local to each application process.
- Measured representative feed/count/title-search SQL shapes over 10,000 synthetic articles in a rolled-back transaction in the isolated recovery database. Feed used the existing ordered index (0.120 ms execution), visible count took 3.382 ms, and substring search took 8.731 ms. All inserts rolled back; three original articles remain. These single local samples do not certify production latency/load or all feature queries. Plans: artifacts/phase9-feed-query-plans.txt.
- Phase 9 remains in progress. The Figma reference is available as a guest canvas, but inspection requires sign-in. Exact design parity, full contrast/screen-reader/cross-browser and pointer review, broader query/load measurements, multi-instance throttling configuration, production SMTP/storage/monitoring configuration, hosting selection, and deployment remain open.


### 8 October 2026 — Blog image rendering

- Added HTTPS Markdown image rendering with encoded URLs/alt text, credential/IP/local-host rejection, lazy loading, no-referrer requests, and responsive sizing. Raw HTML remains encoded; image syntax inside inline/fenced code remains literal.
- Updated the existing demo middleware article's private working copy: replaced its pipeline-image placeholder with the user-provided Microsoft documentation image URL. Other article content and the published snapshot were preserved.
- Live saved preview loaded the diagram successfully: complete=true, natural size 600 × 384; rendered width approximately 495 px at the default viewport and 300 px at a 390 px mobile viewport. Mobile document width remained 390 px. Temporary viewport settings were reset.
- Docker build: zero warnings/errors; non-database tests 55 passed, PostgreSQL tests 43 passed, and client tests 27 passed. Total: 125, no failures or skipped tests. PostgreSQL image persistence/public-snapshot assertions are part of the existing blog lifecycle scenario.
- Proof: artifacts/devsync-blog-pipeline-image.png and artifacts/devsync-blog-pipeline-image-mobile.png. The saved private preview remains open for review; republishing is an explicit author action.


### 8 October 2026 — Accessibility baseline fixes

- Improved muted-text contrast in 14 views and adjusted the shared slate-500 token for the pale canvas. Form controls have stronger visible boundaries and explicit keyboard outlines; anchor/focus scroll margins account for the sticky header.
- Mobile navigation now focuses its first destination on opening; Escape closes it and restores toggle focus. The account layout main region supports explicit skip-link focus. Operating-system reduced-motion styles now suppress animation/transitions as well as smooth scrolling.
- Verified navigation keyboard behavior, account recovery skip focus, visible 2 px action outlines, labels on settings/profile/recovery controls, and 1440 px/390 px settings reflow in the live browser. No preferences, account fields, or article publication states were changed.
- Release build/publish passed with zero warnings/errors. Non-database suite: **55 passed**. PostgreSQL suite: **43 passed**. Client suite: **30 passed**, including three navigation regressions. Total: **128**, no failures or skipped tests.
- Docker is healthy and the demo remains signed in. Screenshots: artifacts/devsync-accessibility-desktop.png and artifacts/devsync-accessibility-mobile.png. Temporary viewport settings were reset.
- See ACCESSIBILITY-CHECKS.md for measurements and scope. Full screen-reader, dynamic contrast, zoom/reflow, OS preference, pointer/cross-browser, exact Figma, and production-host reviews remain pending; P9-03 is not marked complete.

### 8 October 2026 — Personal and team image libraries

- Added a personal image library and a library inside each team, with PNG/JPEG uploads, gallery previews, full-image links, copyable absolute URLs, and portable Markdown snippets. Blog/team editors link to their libraries.
- Uploads start private. Personal ownership and current team membership govern image reads. Team viewers read/copy; writers upload; only personal owners or team owners/admins manage public links. Versioned visibility changes, revocation, no-store image responses, antiforgery, safe MIME handling and bounded uploads are enforced.
- Added PostgreSQL ImageLibraries migration (ninth migration), bytea persistence, scope/content constraints, quota locks and metadata-only pagination. Limits: 5 MiB, 20 million pixels, 100 images per library, 24 gallery entries per page.
- Release build/publish: zero warnings/errors. Non-database tests 58 passed; PostgreSQL tests 46 passed; client tests 30 passed. Total: 134, no failures or skipped tests. New tests cover file structure, uploaded Markdown URLs, private/public/revoked access, team roles, stale visibility, invalid uploads, concurrent quota enforcement, pagination and antiforgery.
- Live browser: uploaded a private 480×240 PNG personally and a private JPEG to DevSync builders. Both gallery previews loaded. URL and Markdown copy buttons worked; relative and copied absolute references rendered in a new private blog preview, and the team JPEG rendered in a new internal article. Existing middleware draft and published snapshot were preserved. No live image public links were enabled.
- Proof: artifacts/devsync-image-library.png, artifacts/devsync-uploaded-image-blog.png, artifacts/devsync-team-image-library.png, artifacts/devsync-team-uploaded-image-preview.png. See IMAGE-LIBRARIES.md for workflow and format/storage boundaries. Phase 9 remains in progress.
- Final Docker rebuild completed with healthy web/PostgreSQL services. Both uploaded images remained readable in their galleries; the database retained nine migrations and two private image records (zero public image links).

### 8 October 2026 — Note code colors and formatting

- Added a bundled Monaco color editor and saved-preview syntax highlighting to personal note code blocks, including distinct C# modifiers/keywords, class declarations, methods, identifiers, comments, strings and numbers. Added language suggestions and an accessible plain-text fallback.
- Added C# formatting with Roslyn and browser-local JavaScript/TypeScript/JSON/HTML/CSS formatting with Prettier. Invalid syntax returns an error without changing source; stale results cannot overwrite newer edits, changed languages or deleted blocks. Formatting integrates with autosave and undo boundaries; source code is never executed.
- C# formatting checks note ownership, archived status, antiforgery, input limits and cancellation. No migration or public visibility change was needed. Existing notes remain private.
- Release build/publish: zero warnings/errors. Non-database tests 62 passed; PostgreSQL tests 47 passed; client tests 34 passed. Total: 143, no failures or skipped tests. Dependency audit returned zero vulnerabilities after pinning the patched DOMPurify override.
- Browser verified dynamic block installation, plain-text/color editor switching, C# and JavaScript formatting, distinct token colors and a saved C# preview. The existing Angular note's invalid property returned a syntax diagnostic and retained its exact source. A new private Code formatting example note was saved for review.
- Final browser connection became unavailable during keyboard Undo verification, preventing that check and screenshot capture. Class declaration coloring and expanded C# wrapping were then checked by the successful final build and formatting regressions; a final visual review remains pending. Docker web/PostgreSQL services are healthy. See NOTE-CODE-EDITOR.md. Phase 9 remains in progress.

### 8 October 2026 — Configurable Gmail SMTP and invitation delivery

- Added Email:Delivery Auto/Smtp/File selection so Gmail SMTP can be enabled in local Development without changing hosting/security mode. Docker maps ignored .env settings for sender, SMTP credentials and the configured application origin. Local file delivery remains the default; production file delivery is rejected.
- Team invitations now prepare an email after their transaction commits, using the same adapter as account verification/resend and password recovery. Messages contain the team, role, configured-origin acceptance link, expiry and registration/verification instructions. Only successful invitation creation sends mail. Failures retain the invitation and manual link with a clear status; logs omit bodies, addresses, credentials and tokens. SMTP has a 30-second cancellation deadline.
- Added GMAIL-SETUP.md and updated account/team workflows and .env.example. Credentials were not supplied, so real Gmail delivery remains unconfigured and no external emails were sent. Production hosting and live-provider inbox verification remain open.
- Release build/publish: zero warnings/errors. Non-database tests 69 passed, PostgreSQL tests 48 passed, client tests 34 passed: 151 total, no failures/skips. Tests cover delivery-mode selection, configured-origin invitation emails and persistence after SMTP failure, alongside existing verification/recovery flows. Docker rebuild completed; readiness is Healthy. No migration or development-data reset was needed.


### 28 October 2026 — Navy and emerald theme refresh

- Replaced lavender brand tokens with emerald/teal, a mint canvas and navy ink. Added the missing intermediate brand shades so card hover borders render consistently.
- Introduced a deep navy navigation panel, mint active states, a navy/teal hub hero, coordinated account illustration panel, teal favicon, softer card shadows and polished form buttons. Shared tokens carry the palette across notes, tasks, teams, blogs and settings.
- Browser reviewed the hub, notes/cards/input focus and desktop sign-in. At 390 px, hub and notes had matching viewport/document widths; mobile navigation opened, Escape closed it and focus returned to its toggle. Restored the normal viewport and left the preview open. Proof: artifacts/devsync-emerald-theme-desktop.png and artifacts/devsync-emerald-theme-notes.png.
- Measured contrast: white primary-button text 4.99:1, muted canvas copy 5.06:1, active navigation 7.32:1 and input boundaries 3.14:1. Reduced-motion rules remain in force. This is a targeted theme check, not a full accessibility audit.
- Release build/publish passed with zero warnings/errors; 69 application tests and 34 client tests passed. No schema or data changes were needed. Docker startup initially encountered a native localhost connection string in appsettings; a Compose-only empty connection-string override now allows the container PostgreSQL components to resolve without changing native settings. Docker web/PostgreSQL are healthy.


### 29 October 2026 — Shared block editors, free Tiptap and dropdowns

- Blogs now provide Markdown, Study Notes blocks, and a full-article Tiptap editor. Reused the note block partial, shared autosave/reordering controls and Monaco code editor/formatter. Each blog mode retains its own draft body; switching selects the preview/publication format without converting or erasing other draft bodies.
- Added Tiptap rich-text blocks to Study Notes alongside the existing editors. Free MIT-licensed Tiptap 3.32.0 core/PM/StarterKit/Image/TaskList/TaskItem provide formatting, headings, lists, checklists, quotes, code, links, URL images and Undo/Redo. No paid packages, subscriptions, cloud credentials or runtime CDN are used. The existing sanitizer override was updated to DOMPurify 3.4.17; npm audit reports zero vulnerabilities.
- Stored rich text as structured JSON and added a server-side node/mark/nesting/URL allowlist with encoded HTML rendering. Publishing retains a separate active-format snapshot. Permission checks protect linked private drafts, ownership, moderation and stale versions. Rich team copies retain formatting and omit private article-link blocks; team editing reuses the same rich component.
- Added the additive RichTextEditors and TeamRichTextCopies migrations (11 total). Existing articles/team items default to Markdown; no old bodies or notes were rewritten. A local owner-readable pre-change database backup is kept under ignored artifacts.
- Dropdown controls now have emerald icons/focus states and themed rounded desktop pickers where supported, with native accessible mobile/browser fallback. Toolbars wrap, and fieldsets shrink around code/rich editors. Verified 390 px document widths of 390 px for both note and blog editors after correcting the fieldset minimum size.
- Final Release build/publish: zero warnings/errors. Application tests 77 passed; PostgreSQL tests 52 passed; client tests 35 passed. Total 164, no failures or skips. New regressions cover safe rendering, rich round trips, independent draft bodies, empty publication validation, published snapshots, private links, team copies and autosave/publish coordination.
- Browser verified new private blog/note creation, Markdown retention, rich formatting and image reload, C# formatting/colors in blog blocks, rich block movement, manual saving, saved previews, Undo, source/visual switching, pending URL panel safety and responsive layouts. Existing user notes, working blog drafts, publication states and SMTP configuration were retained.
- Proof: artifacts/devsync-editor-dropdown.png, artifacts/devsync-tiptap-blog.png and artifacts/devsync-tiptap-note.png. Docker web/PostgreSQL readiness is Healthy. See RICH-EDITORS.md for usage and boundaries. Phase 9 remains in progress.

### 29 October 2026 — Project-wide light and dark themes

- Added an accessible sun/moon toggle at the far right of the shared header, after Sign out or Sign in, and to account pages. Browser preference persists across navigation/reloads and synchronizes between tabs; system appearance is used until a choice is saved. The theme initializes before CSS to avoid a light flash.
- Shared emerald input borders, hover/focus states, native pickers, checkboxes/radios and autofill styling now coordinate with both palettes. Dark navy surfaces and mint text accents cover cards, menus, validation/status notices, settings, teams, tasks, blogs and notes. Images retain their original appearance.
- Tiptap toolbars and surfaces follow the palette. Monaco updates its background/foreground on the theme event without changing editor models, form values or autosave state. Code previews use the same palette.
- Browser verified theme persistence, light/dark switching, notes, settings, team list, Kanban, rich article editor, dropdown picker, sign-in and authenticated/anonymous headers at 320 px without document overflow. Note text, rich JSON and code stayed unchanged during switching. Temporary viewport overrides were reset.
- Release build/publish passed with zero warnings/errors; 77 application tests and 40 client tests passed, including five theme preference/system/storage regressions. No schema changes. Input boundary contrast is 3.39:1 in light mode and 4.35:1 in dark mode; primary white button text is 5.36:1 in dark mode. These are targeted checks, not a complete accessibility audit.
- Proof: artifacts/devsync-dark-mode.png. Theme preference is local to this browser/origin, not synchronized to the account on other devices.


### 29 October 2026 — First Git push and Oracle live test deployment

- Created the first source commit on main and pushed to the user-provided GitHub repository. Removed the native database connection from shareable appsettings; credential configuration, cloud/local backups, generated assets and SSH keys remain ignored. Private key files were never committed.
- Added production Compose, an existing-host proxy variant, reviewed Caddy IP-site fragments and a twice-daily certificate renewal service/timer. Existing Oracle domain app and loopback 8080 service were preserved; DevSync binds only loopback 5180 and PostgreSQL has no public port.
- Deployed on Oracle ARM64 Ubuntu at https://129.159.235.143/ using fresh production PostgreSQL and durable upload/key volumes. User explicitly approved copying existing SMTP and new production database credentials to this server via SSH. Files remain protected and excluded from Git. No local user data was published or migrated.
- Issued a trusted six-day Let's Encrypt IP certificate after staging validation; configured default_sni for IP clients, validated Caddy and tested renewal. Corrected health checks to send the allowed Host header. Production containers and public readiness are healthy.
- Oracle Release build/publish passed with no warnings/errors, 77 application tests and 40 client tests. Browser verified home/sign-in/register over trusted HTTPS; SMTP TLS and authentication passed without sending mail. Live verification/invitation delivery and signed-in acceptance tests are left for the user's own new account. Host OS modernization and an off-server backup plan remain release hardening work. See deploy/README.md.

### 29 October 2026 — Registration retries, complete Markdown and document covers

- Registration now validates the complete password/confirmation request before identity/profile writes and prevents duplicate browser submissions. Native required/format checks and a matching-password check provide immediate correction before submission. Retrying an existing unverified registration with the same email, handle and correct password resumes verification without account duplication or password/profile changes. Added resend-verification guidance. Database regressions prove weak-password rejection leaves no account/profile, corrected details can be reused, pending retry creates only one identity, and an incorrect password cannot resume another registration.
- Replaced the minimal Markdown dialect with BSD-licensed Markdig 1.5.0, pinned in lock files. Tables, bold/italic/strikethrough, nested/numbered lists, quotes, task lists, rules and heading anchors now render with responsive themed table styling. Retained inactive raw HTML and custom safe image/link renderers, including same-origin private media support and unsafe URL tests. Existing Markdown bodies are unchanged; published documents use the new renderer automatically.
- Added optional shared cover-image upload/replacement/removal controls to blog and study-note editors. Reused personal image-library validation, storage and quota. Real bytes determine supported PNG/JPEG type; covers remain private in the library. Dedicated blog-cover routes expose only the published snapshot; draft replacements/removals wait for republishing. Note-cover routes enforce ownership. Added nullable reference columns and indexes/FKs in DocumentCoverImages (migration 12); no existing bodies/images were rewritten.
- Updated Explore, bookmarks, public-profile, My blogs and note cards with landscape thumbnails. Read and private-preview pages also display the selected cover. Failed uploads retain the current selection; pending uploads block manual submission; successful selection/removal participates in existing autosave/version handling. Removing a cover preserves the library image for other references.
- Verified Release build and 79 application tests, 54 PostgreSQL tests, and 48 client tests: 181 passed with no failures/skips. Tests cover cover upload/reload/replace/remove, snapshot isolation, image ownership, private note access, CSRF submission, failure handling, and registration retry. EF tests caught a generated snapshot packaging mismatch before deployment; synchronized the snapshot to the migration's exact generated target model.
- Browser verified the existing private sample cover loads at 480 px, saved cover removal retains other note blocks, blog/note thumbnails render, and note controls fit a 390 px viewport without overflow. Temporary viewport was reset. Browser file selection itself was blocked by the Chrome extension's unavailable file-URL permission; actual upload round trips were tested through the HTTP/PostgreSQL suite. Existing sample images were used only on previously created local private test documents. No local test data was published.
- See COVER-IMAGES-AND-MARKDOWN.md for usage/privacy details. Rollout uses the existing Oracle stack with a pre-change database/key-volume backup and retained application image. Live validation is recorded after rollout.
- Oracle rollout completed with retained previous image and root-only backup `/var/backups/devsync/before-covers-20261029T075931Z` (database archive index checked plus web-data/key volume). Public readiness returned Healthy. Browser verified the supplied RxJS article now contains 3 tables, 7 bold elements, 7 ordered lists and working targets for every table-of-contents link. At 390 px the page remains 390 px wide while tables scroll inside their reading column. No live article content or accounts were edited for verification.
- Final browser inspection also prompted native required/format checks and immediate password-confirmation matching on registration; these supplement the server rules and have a correction/retry regression. Final count: 79 application, 54 PostgreSQL and 48 client tests, all passing. Proof: artifacts/devsync-markdown-table-live.png and artifacts/devsync-note-cover-card.png.
