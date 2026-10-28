# Release preparation and recovery

Status: Oracle live test deployment completed 29 October 2026 at https://129.159.235.143/ . See `deploy/README.md` for the actual topology, certificate renewal and update commands. Local Development Docker is also retained. Signed-in acceptance tests, actual email delivery, OS modernization, off-server backup planning, full accessibility/cross-browser review and representative load testing remain broader release work.

## Reproducible verification

The GitHub workflow in `.github/workflows/verify.yml` restores locked dependencies, compiles Tailwind, builds the solution, runs client and HTTP/PostgreSQL tests, and publishes the web output. It uses a fresh PostgreSQL 17 service with an isolated test password and read-only repository permissions. The workflow is prepared locally; it has not run on GitHub because this project has not been pushed there.

Local Docker verification:

```sh
docker build --target build -t devsync-build:local .
npm run db:test
docker compose up -d --build --wait
curl --fail http://localhost:5180/health/ready
npm audit
dotnet list DevSync.slnx package --vulnerable --include-transitive
```

The build stage runs non-database and client tests. PostgreSQL tests require the dedicated database ending in `_tests`; they apply migrations and remove only their fixture data. Dependency advisories must be checked again at release time.

## Production configuration

Set these values through the chosen host's configuration/secret store. Do not commit real credentials:

| Setting | Required value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | Production PostgreSQL connection, with host-required TLS and restricted database access |
| `AllowedHosts` | Exact application host names separated by semicolons; no wildcard |
| `Account__PublicBaseUrl` | HTTPS origin matching an allowed host, without credentials/query/fragment/subpath |
| `DataProtection__KeyPath` | Absolute directory on durable storage, writable by the app user |
| `Email__From`, `Email__Smtp__Host` | Configured sender and SMTP service |
| `Email__Smtp__Port` | SMTP STARTTLS port, normally 587 |
| `Email__Smtp__Username`, `Email__Smtp__Password` | Provider credentials when required |
| `ReverseProxy__KnownProxies__0` | Actual directly connected trusted proxy IP, if hosting behind a proxy |

Production startup fails for missing durable key configuration, wildcard/missing allowed hosts, invalid public HTTPS origin, or missing SMTP host/sender. Identity and antiforgery cookies require HTTPS. Durable keys use a stable DevSync application name, with reuse verified across separate hosts. The directory must be protected by host permissions, encrypted storage, and restricted backup access; file persistence alone does not encrypt key material at rest. Keep the key ring with the database recovery plan so restored accounts retain decryptable cookies/tokens.

Only configured proxy IPs may supply forwarded client IP/scheme headers; absent configuration disables forwarding. Configure the gateway to replace incoming forwarded headers and forward the original HTTPS scheme. The application processes forwarding before redirects/authentication. This follows [Microsoft's proxy guidance](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).

The gateway terminates TLS. Restrict the backend HTTP port to the gateway and monitoring network. `/health/live` and `/health/ready` support direct internal HTTP probes; other production HTTP requests redirect to HTTPS. Public user-facing health responses reveal no credentials or detailed database diagnostics.

The current `compose.yaml` intentionally remains a localhost Development configuration with local email delivery. Do not expose it as a production deployment. Hosting topology, TLS certificate issuance, SMTP provider delivery, encrypted key storage, and production credentials must be supplied and verified for the chosen environment.

## Migration and rollout

1. Build an immutable image and record its digest plus the previous working digest.
2. Run all regressions and dependency checks; verify release gates below.
3. Create and verify a database backup and preserve the durable key ring using the host's encrypted backup system.
4. Review pending EF migrations, including compatibility with the previous application version. Apply migrations once through an explicit deployment job using `dotnet DevSync.Web.dll --initialize-database`. This also provisions application roles without seeding accounts.
5. Start the new image with production settings and durable key storage. Wait for readiness; verify HTTPS sign-in, account email delivery, a private editor save, and permissions.
6. Monitor health, application errors, SMTP delivery, database connections/locks, disk capacity, and backup freshness. Keep old images and the verified backup according to the chosen retention policy.

HTTP serving never migrates the schema. The local Compose initializer already separates migration from web startup; production should use the same explicit separation with deployment credentials distinct from restricted runtime credentials.

## Local database backup and restore rehearsal

```sh
npm run db:backup -- artifacts/backups/devsync-YYYYMMDD.dump
npm run db:restore-test -- artifacts/backups/devsync-YYYYMMDD.dump devsync_YYYYMMDD_restore_tests
```

These scripts use the existing local PostgreSQL Compose service and its configured database/user. Custom-format backups stream directly to a new file created with mode 0600. Existing backup files are never overwritten. Restoration requires a new database name ending in `_restore_tests`; `createdb` rejects an existing destination. The restore uses one transaction, exits on errors, and never runs `--clean`, drop, or overwrite commands. A failed restore leaves the newly created database for diagnosis. This uses [PostgreSQL custom-format backup support](https://www.postgresql.org/docs/17/app-pgdump.html).

Verified rehearsal: `artifacts/backups/devsync-phase9.dump` restored into `devsync_phase9_restore_tests`. Development and recovery copy both contained two accounts, three articles, one team, and eight applied migrations. The copy had 113 validated constraints and zero unvalidated constraints. The archive has mode 0600. The original database and live app remained intact. The isolated recovery database is retained for inspection; do not point the web service at it during ordinary use.

Production recovery must use the host's credentials, encrypted backups, and isolated infrastructure. Restore into a fresh database first, validate schema/content/permissions, and test the application before routing traffic. A database dump does not include external SMTP configuration, TLS certificates, or the Identity key ring.

## Rollback and monitoring

For a code-only regression with a schema compatible with the previous image, stop rollout and run the recorded previous image against the preserved database/key ring, then verify readiness and key user workflows. Do not blindly reverse migrations after new writes. If schema/data restoration is necessary, pause writes, restore the verified backup into a separate database, validate it with the compatible image, and switch connections/traffic only after review. Document the accepted recovery point and treatment of writes since the backup. A production rollback rehearsal remains pending on the chosen host.

Application logs go to container stdout with framework request noise reduced; account-email failure logs omit tokens/bodies/credentials. Forward logs to the chosen monitoring service with access/retention controls. Alert on readiness failures, elevated 5xx/429 responses, repeated email failures, PostgreSQL connection saturation/deadlocks, low disk space, and overdue/failed backups. Provider-specific alerts have not been configured.

## Query and throttling baseline

A rollback-only transaction inserted 10,000 synthetic articles into the isolated recovery copy, with 20% hidden, and measured representative public-feed/count/title-search SQL shapes. The first-page feed used the existing PublishedAt/Id index in reverse order and executed in 0.120 ms; visible count took 3.382 ms; a selective title substring search took 8.731 ms. All synthetic inserts were rolled back, leaving the original three articles. Plans are recorded locally in artifacts/phase9-feed-query-plans.txt. These are single-run database execution measurements on a local warm dataset, not end-to-end latency or production load guarantees. Full feature query/load measurements remain pending; this sample does not justify another index or cache.

Unsafe HTTP requests have a fixed-window limit of 120 per minute per authenticated account, or per client IP for anonymous requests, configurable with Writes:RequestsPerMinute. Account routes retain their separate 30-per-minute allowance. Reads and health probes are exempt. Error-page re-execution does not consume a second write allowance. Limits are per process; multiple application instances need an ingress/distributed rate-limit policy for a shared deployment budget.

See [accessibility checks and remaining review](ACCESSIBILITY-CHECKS.md) for contrast, focus, labels, navigation, and reduced-motion changes.

## Remaining release gates

- Inspect exact Figma fonts, tokens, assets, mobile variants, and interactions. The current reference is available in guest view, but inspection requires sign-in; pixel parity has not been certified.
- Complete keyboard/screen-reader/contrast review and manual pointer drag/drop across supported browsers.
- Measure representative query plans and response times using a realistic data volume.
- Choose shared ingress/distributed throttling for a multi-instance host and tune budgets under representative traffic. Feature ownership, antiforgery, mutation limits, size limits, and safe rendering are tested. Bounded personal/team PNG and JPEG uploads are enabled; see [image library rules](IMAGE-LIBRARIES.md). Include image bytes in database capacity, backup and host-level upload-scanning decisions.
- Verify production SMTP delivery, TLS/proxy topology, protected durable storage, backups, observability, and rollback on the selected host.
- Choose hosting and authorize production deployment after the candidate is reviewable.
