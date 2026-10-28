# Profiles, settings, verification, and moderation

Functional baseline verified on 8 October 2026. Exact Figma assets, components, and visual parity remain in phase 9.

## Settings and public profiles

Confirmed Members use `/account/settings` to choose public-profile visibility, bio visibility, reduced interface animation, and 12 or 24 articles per Explore/Saved page. Defaults are private profile, hidden bio, ordinary animation, and 12 articles. Saving derives the account from claims and checks a version token; stale forms return HTTP 409 without adopting another version.

`/people/{handle}` is public only after explicit opt-in. It exposes display name, handle, approved publisher badge, optionally the bio, and paginated visible published article snapshots. Email, private working edits, notes, tasks, team membership, and verification evidence are excluded. Disabling the profile returns HTTP 404 for its public URL. Reader author links appear only for enabled public profiles.

## Publisher verification

`/account/verification` accepts a public HTTPS portfolio/professional-profile URL and a statement of 1–2,000 characters. URLs must have a public DNS-style host, no embedded credentials, and fit 2,048 characters after normalization. Localhost/local URLs are rejected. The app stores the URL but does not fetch it or accept evidence uploads. Evidence and statements are restricted to the applicant and authorized platform reviewers.

Each applicant has at most one pending or approved request. Pending requests can be approved or rejected; approved requests can be revoked. Rejected/revoked applicants may submit a new request. Submission checks the most recent request version; reviewer actions check the reviewed request version. Account locking and a filtered unique index prevent competing active requests. Reviewers cannot review their own requests, and every decision requires a reason.

Approval displays a publisher badge on visible public profiles and public article readers. Revocation removes it. Confirmed Members can publish independently of this badge.

## Reports and moderation

Signed-in Members can report visible published articles or visible comments from the reader. Reports require a category and 1–2,000 characters of explanation. Categories are spam, abuse, unsafe, copyright, and other. Retrying a pending report by the same reporter against the same target does not create a duplicate. Private drafts and comments belonging to another article cannot be reported through these routes.

`/moderation` and its request/report detail routes require the platform Moderator policy, which also admits Administrators. Team Owner/Admin roles do not grant platform moderation access. The local demo remains a Member; initialization provisions roles without creating privileged accounts.

Reviewers can dismiss pending reports, hide visible targets, or restore hidden targets. Each action requires a reason, the report version, and the target version when changing visibility. Changes and audit records commit together. Stale forms return HTTP 409 with actions paused until reload. Report excerpts are bounded snapshots; a deleted comment retains its target ID and excerpt for review, and a reviewer may dismiss the remaining report.

Hidden articles disappear from Explore, Home feeds, bookmarks, public profiles, public readers, and public article links in personal notes/tasks. New reactions, bookmarks, comments, and publication of a hidden article are rejected. Owners can still view and save their private working copy; the editor and dashboard explain the hidden state. Restoring resumes public visibility. Hiding a comment removes it from public reader results; restoring returns it. Independently shared team copies retain their own content and permissions.

Audit records retain action, reason, UTC timestamp, target ID, and actor association. Removing an actor clears the association without deleting the audit. Histories show the latest 50 actions; the queue pages pending or reviewed reports/requests, 20 of each per page.

## Acceptance and verification

- Public opt-in, bio consent, disable behavior, pagination, reduced motion, validation, and stale settings.
- Private evidence, valid/invalid URLs, rejection/resubmission, approval/revocation, self-review denial, and competing decisions.
- Report retries, target ownership, hidden article/comment visibility, author publication rejection, restore, deleted-comment retention, and audit history.
- Anonymous/member role boundaries, antiforgery rejection, stale report/target versions, and no audit writes for failed actions.

Five governance PostgreSQL/HTTP scenarios pass alongside all earlier feature tests. Full totals: 36 HTTP/configuration/scheduler tests, 43 PostgreSQL tests, and 27 client tests. Fixture data is removed from the dedicated test database. Live Docker settings and verification pages were reviewed at 1440 px and 390 px without horizontal overflow; existing demo preferences and content were preserved. Privileged decision workflows were exercised in isolated automated tests, without granting the runtime demo a privileged role.
