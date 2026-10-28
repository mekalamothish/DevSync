# Account milestone

## Implemented flows

| Route | Behavior |
|---|---|
| `/account/register` | Collect display name, handle, email, and password; create Identity user and profile transactionally; assign Member; send verification link. |
| `/account/sign-in` | Require a verified email; validate the password; optionally persist the session; accept local return URLs only. |
| `/account/sign-out` | POST only, authenticated, with an antiforgery token. |
| `/account/confirm-email` | Validate an Identity confirmation token and show success or an invalid-link message. |
| `/account/resend-verification` | Send another verification link when eligible; always use the same public acknowledgement. |
| `/account/forgot-password` | Send recovery mail only to a confirmed account; unknown and ineligible addresses receive the same public acknowledgement. |
| `/account/reset-password` | Validate the Identity recovery token, update the password, and rotate the security stamp. The token cannot be reused after success. |
| `/account/profile` | Member-or-higher only; edit the current user's display name, lowercase handle, and plain-text bio; reject stale versions. |

No external login buttons are presented until those providers are configured. Public developer profiles and publishing-policy enforcement are later feature tasks.

The initial blog publishing policy for phase 4 allows confirmed Members to draft and publish; publisher verification is a separate phase-8 badge/workflow.

## Rules

- Passwords require 12–128 characters, uppercase and lowercase letters, a number, and a symbol.
- Email confirmation is required before sign-in. Confirmation and recovery links expire after two hours.
- Five failed password attempts lock an eligible account for 15 minutes; sign-in failures use a generic message.
- Account routes share a per-IP limit of 30 requests per minute, including GET requests, configurable through `Account:RequestsPerMinute`. Reverse-proxy IP handling is a release-preparation task.
- Handles have 3–40 ASCII letters, numbers, or underscores and start with a letter. They are stored in lowercase and protected by a database check and unique index.
- Normalized emails are unique in PostgreSQL as well as validated by Identity.
- A posted user ID cannot choose the profile to edit; the handler receives the authenticated user ID.
- Profile versions detect stale forms and intervening writes. A conflict preserves submitted values and asks the user to review before retrying.
- The session cookie is HTTP-only, SameSite=Lax, and secure-only outside Development. Remember-me sessions have a 14-day cookie lifetime; ordinary sessions use a browser-session cookie. Server-side security-stamp validation occurs at most five minutes after a password reset.
- Member, Moderator, and Administrator roles are provisioned idempotently. Registration grants Member only. No administrator credentials or automatic elevated accounts are created.

## Local email delivery

Development defaults to `FileAccountEmailSender`; emails are saved under `src/DevSync.Web/.local/mail/*.eml` (Docker: `/app/.local/mail` in its persistent volume), outside static assets and ignored by Git. On Unix, files are created with owner-only read/write permissions. Open the relevant local mail file and follow its link to complete verification or recovery. These files contain private tokens; keep them local. Set `Email:Delivery=Smtp` to send real emails in Development. See [Gmail and Docker setup](GMAIL-SETUP.md).

Configure `Account:PublicBaseUrl` if the development port changes. The development default is `http://localhost:5180`. Links use this configuration rather than incoming Host headers.

## Production mail adapter

`Email:Delivery=Auto` selects SMTP outside Development and local files in Development. Explicit `Smtp` enables SMTP in either environment; `File` is allowed only in Development. Configure:

```text
Account__PublicBaseUrl=https://your-application.example/
Email__From=accounts@your-application.example
Email__Smtp__Host=smtp.your-provider.example
Email__Smtp__Port=587
Email__Smtp__Username=<configured outside source control>
Email__Smtp__Password=<configured outside source control>
```

SMTP uses TLS. No external email was sent during development verification; tests substitute an in-memory mailbox. Actual provider credentials, delivery, and production key persistence must be configured and verified before deployment. Mail delivery failures log the user ID and failure type without tokens, bodies, or credentials. Recovery acknowledgement remains generic; users can retry via the resend flow.

## Acceptance checks

- Account forms render with accessible labels, server validation, and non-cacheable responses.
- Registration, confirmation, sign-in, profile editing, and sign-out work using HTTP requests and real PostgreSQL.
- Invalid passwords and duplicate identifiers do not leave incomplete users/profiles.
- External return URLs cannot redirect authenticated users to another origin.
- Missing antiforgery tokens fail before database writes.
- Unauthenticated profile requests challenge sign-in; Member cannot satisfy Moderator or Administrator policies.
- Recovery acknowledgement does not disclose whether an account exists; recovery-token replay fails.
- Profile conflicts are detected, and bio content is HTML-encoded in forms.

The layout follows the observed DevSync palette and structure. Exact Figma token/font/component parity remains part of the detailed design review.
