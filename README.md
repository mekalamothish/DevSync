# DevSync

A developer knowledge and productivity workspace based on the supplied [Figma design](https://www.figma.com/design/we7rb1RMwsyrHybfpw3COl/Untitled?node-id=0-1).

**Stack:** ASP.NET Core MVC / .NET 10, vertical feature slices, Razor views, PostgreSQL, EF Core / Npgsql, and Tailwind CSS 4.

The application provides a responsive home hub, Identity registration/sign-in/sign-out, email verification, password recovery, profile editing, blogs with drafts/publishing and reader actions, private study notebooks with an autosaving block editor, revision cards with graded scheduling/history, personal tasks with list/Kanban views, and private teams with invitations and shared workspaces. Upcoming product modules are tracked in [the phased backlog](docs/PHASES.md).

## Run with Docker (recommended local startup)

Docker builds the entire .NET solution, runs HTTP/rendering and editor tests, compiles Tailwind, and publishes a clean runtime image. You do not need a host .NET SDK or Node installation to build/run the stack if you already have a `.env` file.

```sh
# First setup only: create .env with a random password (requires Node).
npm run local:init

# Build, initialize PostgreSQL schema/roles, and start the application.
docker compose up -d --build --wait
```

Open **http://localhost:5180**. PostgreSQL, `db-init`, and `web` are part of the same Compose project. The database initializer exits successfully after applying migrations and provisioning roles; its exited state is expected. The web service starts only after the database is healthy and initialization has succeeded. PostgreSQL and the web app bind their published ports to localhost.

If Node is unavailable, copy `.env.example` to `.env` and replace the placeholder with a strong local database password before the first launch. Existing `.env` files/database volumes are preserved. Optional `WEB_PORT` defaults to 5180; change it if another process already occupies that port. Stop a native `dotnet run`/watch process before using the same port for Docker.

```sh
docker compose ps --all
docker compose logs --tail=100 web db-init
docker compose stop
```

`npm run docker:up`, `docker:logs`, and `docker:stop` provide the same commands. After source changes, repeat the build/start command. Database data stays in `devsync_devsync-postgres`; local verification emails and Identity protection keys stay in `devsync_devsync-web-data` through rebuilds/restarts. The runtime executes as the image's `app` user.

This Compose configuration is for local development over HTTP. It uses Development mode and defaults to saving emails inside the web container. Set `EMAIL_DELIVERY=Smtp` with the settings in [Gmail setup](docs/GMAIL-SETUP.md) for real delivery. Register an account, then inspect your local email using:

```sh
docker compose exec web sh -c 'ls /app/.local/mail'
# Replace MAIL_FILE with the filename for your registration email.
docker compose exec web cat /app/.local/mail/MAIL_FILE.eml
```

Open the verification link before signing in. The initializer creates roles only; it does not create a demo or administrator account. Production HTTPS, SMTP, and deployment configuration remain a separate release task.

### Local demo login

This checkout's local database contains an email-verified Member account, created through registration and local email confirmation:

- Email: `demo@devsync-test.invalid`
- Password: `DevSync-Demo42!`

Sign in at **http://localhost:5180/account/sign-in**. The account persists in the PostgreSQL volume through Docker rebuilds and restarts. It is not automatically seeded into new databases. When switching from a native preview to Docker, open a fresh sign-in page before submitting; an older form can contain an expired antiforgery token.

The [multi-stage build](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/docker/building-net-docker-images?view=aspnetcore-10.0) uses the official Microsoft SDK/runtime images. Startup uses [Compose health and completion dependencies](https://docs.docker.com/compose/how-tos/startup-order/).

## Run natively

Requires .NET SDK 10.0.401 or a newer 10.0 feature band, Node.js 22+, and Docker with Compose.

```sh
npm ci
dotnet restore DevSync.slnx
dotnet tool restore
npm run local:init
docker compose up -d --wait postgres
npm run db:migrate
npm run db:seed
npm run dev
```

Open **http://localhost:5180**. For live style editing, run `npm run css:watch` in another terminal. `npm run dev` starts the .NET watcher and builds the initial CSS.

After building, `npm start` runs the application without rebuilding. Use **Join DevSync** to register, then open the verification email saved under `src/DevSync.Web/.local/mail/` and follow its link before signing in. Local delivery sends no external email by default. [Gmail setup](docs/GMAIL-SETUP.md) explains enabling SMTP for verification, recovery and team invitations. [Account workflow details](docs/ACCOUNT-FLOWS.md) include production SMTP configuration and security rules.

`npm run db:seed` provisions application roles without creating administrator accounts. Registration also provisions the roles safely if needed.

After signing in, open **My blogs → New article** to write a draft. Save and preview it, then publish to the public Explore feed. Later autosaved edits remain private until you publish again. [Blog workflow rules](docs/BLOG-FLOWS.md) document editor syntax, visibility, and conflict handling.

Open **Study notes** to create a notebook or note. Add text, headings, code, checklists, and article links; keyboard controls reorder blocks. Save and open **Saved preview → Create revision card** to write a question and answer. **Revision** shows cards due now, upcoming reviews, and history. Reveal the answer and grade your recall to schedule the next review. Archiving a note pauses its cards until you restore it. [Notes and revision rules](docs/NOTES-REVISION-FLOWS.md) describe privacy, conflicts, limits, and scheduling.

In personal note **Code snippet** blocks, choose **C#** (or another supported language) for syntax colors, then click **Format code**. Colors also appear in the saved preview. [Code editor guide](docs/NOTE-CODE-EDITOR.md) covers supported languages, Undo and the plain-text fallback.

Open **My tasks** to create a personal board and tasks. Set priorities and due dates, add a checklist, and link your notes or permitted articles. **Kanban board** shows the same tasks in To do, In progress, and Done. Drag cards or use the keyboard movement controls; moving into Done completes a task. Archive/restore keeps task activity, and filtered list results support search, sorting, and pagination. Assign tasks to yourself or leave them unassigned; team assignments are available inside team workspaces. [Task workflow rules](docs/TASK-FLOWS.md) describe privacy, date handling, limits, and version conflicts.

Open **Teams** to create a private workspace. Owners/Admins create email-bound invitation links; the intended recipient signs in and accepts. Team members can create and edit notes, internal articles, and assigned tasks, or share an independent copy of their personal content. Viewers have read access. Personal originals remain private. Invitations use the configured email sender; local file mode saves messages without external delivery. [Team workflow rules](docs/TEAM-FLOWS.md) describe roles, ownership transfer, revocation, limits, and conflicts.

Open **Settings** to control public-profile visibility, bio visibility, reduced animation, and article page size. Public profiles are private by default and use /people/{handle} after opt-in. **Publisher verification** accepts a private evidence URL and statement for review; approval grants a badge. Platform Moderators/Administrators use /moderation to review requests and reports, hide/restore public content, and record reasons. The demo remains a Member. [Governance workflow rules](docs/GOVERNANCE-FLOWS.md) describe privacy, transitions, and audit behavior.

The initialization command creates an ignored `.env` with a random local password and never overwrites an existing file. The database scripts read it and pass the connection string through the child process environment. The application itself does not load `.env`; outside these scripts, configure `ConnectionStrings__DefaultConnection` or the `ConnectionStrings:DefaultConnection` user secret.

The database port defaults to `54329` and binds to localhost. Docker stores database data in the `devsync_devsync-postgres` named volume. `docker compose stop` stops the service while keeping its data.

## Verify

```sh
npm run build
npm test
npm run test:client
```

The HTTP tests verify MVC feature-folder discovery, shared layout rendering, health/error behavior, account form rendering, antiforgery protection, and account request limiting.

Create a dedicated database once, then run the PostgreSQL integration test:

```sh
docker compose exec postgres createdb -U devsync devsync_tests
npm run db:test
```

The database suite applies migrations and checks PostgreSQL persistence, uniqueness, accounts, blog snapshots/reader actions, private note ownership/block order/archive, revision scheduling/retries/concurrency, task ownership, movement, checklists, and due dates, plus team invitations, roles, private-copy sharing, revocation, and concurrent edits. It also covers settings, public-profile privacy, verification decisions, moderation visibility, audit retention, and conflicting reviewer actions. The client suite checks autosave races, block mutation/focus, conflict behavior, and board movement without extra browser dependencies. The persistence test rolls back its writes. Integration tests use unique run IDs, capture email in memory, and clean up only their own users/content in the dedicated test database. A database name ending in `_tests` is enforced. `npm run db:test` supplies `DEVSYNC_TEST_CONNECTION` using the configured database name with `_tests` appended.

- `/health/live`: checks that the application is running.
- `/health/ready`: checks PostgreSQL connectivity and that migrations have been applied; returns 503 until the database is ready.

## Build for release

```sh
npm run publish
```

This compiles Tailwind before publishing the application to `artifacts/publish`. Direct `dotnet publish` fails if the compiled stylesheet is missing. Production now validates explicit allowed hosts, the public HTTPS origin, SMTP host/sender, and an absolute durable Identity key directory. Configure the actual trusted proxy IPs before hosting behind a gateway. [The release runbook](docs/RELEASE-RUNBOOK.md) covers CI, configuration, backup/restore, migrations, monitoring, rollback, and outstanding release gates. The localhost Compose stack remains Development; production deployment has not been performed.

Development mail and key files under `.local` are excluded from publishing as well as Git.

## Architecture

- `DevSync.Web`: HTTP controllers, Razor views, and use-case handlers grouped under `Features`.
- `DevSync.Domain`: domain entities and rules, without web or persistence dependencies.
- `DevSync.Infrastructure`: EF Core mappings, migrations, Identity storage, and future external integrations.
- `DevSync.Tests`: HTTP and PostgreSQL integration tests.

Controllers dispatch to feature handlers. Views are discovered at `Features/{Controller}/{Action}/View.cshtml`. Shared layouts live in `Views/Shared`; `_ViewImports.cshtml` and `_ViewStart.cshtml` are at the Web project root so they apply to both feature and shared views.

Each future slice owns its command/query, handler, validation, view model, and view as needed. Handlers can use `AppDbContext` directly. We will introduce shared abstractions only when actual use cases justify them.

Read [the architecture decisions](docs/ARCHITECTURE.md), [screen map](docs/SCREEN-MAP.md), and [phase/task tracker](docs/PHASES.md) before extending the project.

## Personal and team image libraries

Use **My images** or **Teams → your team → Team images** to upload PNG/JPEG files, preview them, and copy image URLs or Markdown for blogs and team content. Images start private; explicit public links support public articles. Limits: 5 MiB, 20 million pixels, 100 images per library. Image bytes live in PostgreSQL and survive Docker rebuilds. See [image library rules](docs/IMAGE-LIBRARIES.md) for roles, visibility, formats, storage and URL behavior.

Blogs now offer **Markdown**, **Study Notes blocks**, and **Rich text · Tiptap** under **Writing editor**. Study Notes adds **Rich text · Tiptap** in **Add a block**. Each blog editor retains its own draft body; the selected editor supplies preview/publication. See the [rich editor guide](docs/RICH-EDITORS.md) for formatting, limits and free packages.
