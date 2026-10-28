# Teams and collaboration

## Sharing model

Teams have their own private workspace at /teams/{id}. Create notes, internal article drafts, and tasks there, or explicitly copy your own personal content into a team. Team copies are independent: later personal edits do not update them, and editing a team copy does not alter its personal original. Team membership never grants access to another member's personal routes, notebooks, revision cards, drafts, or boards.

A copied note becomes Markdown text with headings, code, and checklist content. Personal article-link blocks are omitted. A copied task keeps description, priority, due date, completion state, and checklist text; its personal note/article links and assignment are omitted. Internal team articles do not publish to the public blog feed. Public publishing remains an explicit personal article action.

## Roles

| Action | Owner | Admin | Member | Viewer |
|---|---|---|---|---|
| Read workspace, members, recent activity | Yes | Yes | Yes | Yes |
| Create, share, edit, archive/restore team items | Yes | Yes | Yes | No |
| Receive task assignments | Yes | Yes | Yes | No |
| Create/revoke Member or Viewer invitations | Yes | Yes | No | No |
| Manage Members and Viewers | Yes | Yes | No | No |
| Grant/revoke Admin or remove an Admin | Yes | No | No | No |
| Transfer ownership | Yes | No | No | No |
| Leave team | After transfer | Yes | Yes | Yes |

The owner chooses an existing Admin or Member as successor. Transfer changes the former owner to Admin atomically and keeps one owner. An owner cannot leave or be removed before transfer. Admins cannot promote themselves or manage another Admin. Platform Administrator and Moderator roles do not bypass team membership.

Removing/leaving membership immediately blocks subsequent team requests and clears that person's team task assignments. Changing a writer to Viewer also clears assignments. These changes advance affected item versions so stale editors cannot reinstate removed assignments.

## Invitations

Owners/Admins enter an email and Member/Viewer role to create an invitation. After the invitation transaction commits, its acceptance link is emailed through the configured account-email sender. Development defaults to local files; SMTP sends real emails when configured. The link is also shown once for manual delivery. Email failure preserves the invitation and shows a delivery warning; a new invitation retries delivery and replaces earlier unused links. See [Gmail setup](GMAIL-SETUP.md). Tokens contain 256 random bits; only their SHA-256 hash is stored. Invitations last seven days and require the exact normalized email of a confirmed, signed-in account.

Acceptance is an antiforgery-protected POST. Wrong-recipient, expired, revoked, malformed, and consumed links return 404 without exposing team details. A new invitation revokes previous unused links for the same team/email. Concurrent acceptance has one winner, and a used token cannot rejoin after removal. Managers can revoke pending invitations.

Limits: 20 memberships per account, 50 members per team, and 100 active pending invitations per team.

## Team content and conflicts

Workspace content is paginated in groups of 20, filterable by note/article/task and active/archived state, and bounded to 500 retained items per team. Titles use 1–160 characters; Markdown uses up to 50,000 characters. Archive retains content; restore makes it editable again. Shared task status supports To do, In progress, and Done, with four priorities, date-only due dates, and assignment to a current writer.

All writers can edit all team items. Saving is manual. Item versions protect content/archive changes, and team versions protect membership/invitation actions. Every write locks the team before rechecking current membership, role, target IDs, and assignment eligibility. Membership changes and content writes therefore cannot bypass each other. Stale forms return 409 and retain rejected edits; saving pauses until the writer reloads.

Server-rendered content uses the existing safe Markdown renderer. Titles, member names, invitation emails, and activity text are Razor encoded. No stored HTML is rendered directly. Recent activity keeps the latest 30 entries on screen; it records lifecycle changes without copying private content or invitation tokens.

Ordinary HTTP requests and reloads provide collaboration for this milestone. Real-time subscriptions are not required for these workflows and remain a possible later enhancement.

## Verification

The PostgreSQL HTTP suite follows successful redirects through the Razor destination. It covers email binding, expiration, replacement, revocation, replay and simultaneous acceptance; role boundaries, ownership transfer, leave/removal, and platform-role isolation; safe rendering, private-copy isolation, assignment clearing, archive/restore, stale/concurrent saves, validation, CSRF, pagination, and capacity limits. Test users and their owned teams are confined to the dedicated test database.

The final milestone passed 36 non-database, 38 PostgreSQL, and 27 client tests (101 total). The local browser verified team content, task assignment/dates/status, save/reload, archive/restore, retained validation drafts, and invitation creation/revocation. Desktop and 390-pixel layouts were reviewed; exact Figma parity and full release accessibility checks remain pending.
