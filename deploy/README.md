# Oracle deployment

Separate production stack: PostgreSQL and the application have no public ports. Nginx terminates HTTPS on 443 and redirects port 80. PostgreSQL, uploaded images, account keys and TLS certificates use durable named volumes. Never run `down -v` against this stack.

Copy `.env.example` to `.env` here, use a fresh production database password and configure SMTP. Keep `.env` owner-readable only. This does not transfer local accounts, notes, uploads or backups to the server. The database initializer provisions schema/roles only.

Before deployment, check existing services, available resources and Docker network ranges. `172.30.82.0/24` must not overlap existing networks; if changed, update the proxy's address and `ReverseProxy__KnownProxies__0` together. Oracle network ingress and the host firewall must allow TCP 80/443, while preserving SSH access. Do not expose PostgreSQL or port 8080.

From this directory, bootstrap the ACME webroot:

```sh
docker compose -f compose.bootstrap.yaml up -d
docker compose --env-file .env -f compose.production.yaml run --rm --no-deps certificate certonly --webroot -w /var/www/acme --ip-address 129.159.235.143 --preferred-profile shortlived --cert-name 129.159.235.143
```

Complete Certbot's account/terms prompts. For an IP certificate, use Certbot 5.4 or later and the `shortlived` profile. See [Let's Encrypt's IP certificate guidance](https://letsencrypt.org/2026/03/11/shorter-certs-certbot). Validate against staging first before requesting a trusted certificate. A staging certificate must not be used for live sign-in.

Once a trusted certificate exists:

```sh
docker compose -f compose.bootstrap.yaml stop bootstrap
docker compose --env-file .env -f compose.production.yaml up -d --build --wait
```

Install a host systemd timer to run `renew-certificates.sh` twice daily with persistent catch-up. Renewal reloads Nginx after a configuration check. IP certificates last six days, so renewal and expiry monitoring are required. Verify public HTTPS, certificate SAN/expiry, redirects, sign-in, private saves and email flows after rollout. Run backups of PostgreSQL and web-data before updates; retain both along with the previous application image. Consult `../docs/RELEASE-RUNBOOK.md` for release and recovery requirements.

For a later domain, point DNS to this instance, issue a domain certificate, update PUBLIC_HOST and replace the IP issuance flag with the domain flag. Keep the existing database and web-data volumes.

Status: deployed to the Oracle IP on 29 October 2026 using the existing-host Caddy variant below. The standalone Nginx variant is an alternative configuration and was not activated on this server.

## Existing host Caddy (Oracle instance)

The inspected Oracle instance already serves a separate application on loopback 8080 through Caddy. Preserve its configuration and service. Use `-f compose.production.yaml -f compose.host-proxy.yaml` instead: DevSync binds only loopback 5180; containerized Nginx is disabled. The trusted forwarding address is the Docker bridge gateway. Verify it before deployment.

For this variant, Certbot stores certificates under `/etc/devsync/tls` on the host and serves challenges from `/var/lib/devsync/acme`. Add a separate IP-only Caddy site for the webroot, then issue the IP certificate. Only after issuance add the IP HTTPS block with explicit certificate/key paths and `reverse_proxy 127.0.0.1:5180`. Existing domain sites remain intact. Back up and validate the Caddy configuration before reload. Restrict certificate directories to root and the caddy group, and repeat group/read permissions after renewal. Use a root-owned timer that runs renewal using both Compose files, validates Caddy, then reloads Caddy; the Nginx renewal script above applies only to the container-proxy topology.


### Current live deployment (29 October 2026)

- URL: https://129.159.235.143/ ; source repository: https://github.com/mekalamothish/DevSync . Server checkout: `/opt/devsync`.
- Existing host Caddy and its previous domain app remain in place. The IP site uses the supplied fragments. Merge `caddy.global-options.fragment` into the **single first global block**: `default_sni` is required for IP clients that omit SNI. Always validate before reload; do not overwrite an existing global block. Host Caddy forwards to loopback 5180, and the app trusts only the Docker gateway on its isolated subnet.
- Production has a fresh PostgreSQL database. Schema and roles were initialized without copying local users, notes, uploads, test fixtures or backups. Register a new account and verify its email before signing in. No demo account is provisioned publicly.
- Environment is protected at `/opt/devsync/deploy/.env` (0600); application data and keys are in `devsync-production_web-data`, PostgreSQL in `devsync-production_postgres-data`. Do not remove these volumes.
- Trusted Let's Encrypt IP certificate was issued after staging validation. Certificate files are under `/etc/devsync/tls`, readable by root and the caddy group. `devsync-cert-renew.timer` runs twice daily; its service was tested successfully. IP certificate renewal must remain enabled. Never load the separate staging certificate into live Caddy.
- Production Docker health probes send the configured Host header to satisfy AllowedHosts validation. The public readiness endpoint returned Healthy. Anonymous home, sign-in and registration pages loaded over trusted HTTPS. SMTP TLS/authentication succeeded; no email was sent during validation. Actual verification/invitation delivery and signed-in user workflows still need live user acceptance testing.
- Server build: zero warnings/errors, 77 application and 40 client tests passed. These are not a claim that PostgreSQL integration tests were rerun on the live database. Existing domain still responds after Caddy reload.
- Host is Ubuntu 20.04 ARM64, Docker 28.1.1 / Compose 2.35.1 from the official Focal packages. Upgrade the host to a currently supported Ubuntu release in a separate maintenance window before broader production use; no OS upgrade was attempted alongside deployment.

Update commands (after backing up PostgreSQL and the web-data volume):

```sh
cd /opt/devsync
git pull --ff-only
cd deploy
sudo docker compose --env-file .env -f compose.production.yaml -f compose.host-proxy.yaml build db-init
sudo docker compose --env-file .env -f compose.production.yaml -f compose.host-proxy.yaml up -d --no-build --wait postgres db-init web
```

Inspect without printing secrets:

```sh
sudo docker compose --env-file .env -f compose.production.yaml -f compose.host-proxy.yaml ps
sudo systemctl status devsync-cert-renew.timer
curl --fail https://129.159.235.143/health/ready
```
