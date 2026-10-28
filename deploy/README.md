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

Status: deployment configuration prepared; server installation, certificate issuance and live verification must be completed before this is a live service.

## Existing host Caddy (Oracle instance)

The inspected Oracle instance already serves a separate application on loopback 8080 through Caddy. Preserve its configuration and service. Use `-f compose.production.yaml -f compose.host-proxy.yaml` instead: DevSync binds only loopback 5180; containerized Nginx is disabled. The trusted forwarding address is the Docker bridge gateway. Verify it before deployment.

For this variant, Certbot stores certificates under `/etc/devsync/tls` on the host and serves challenges from `/var/lib/devsync/acme`. Add a separate IP-only Caddy site for the webroot, then issue the IP certificate. Only after issuance add the IP HTTPS block with explicit certificate/key paths and `reverse_proxy 127.0.0.1:5180`. Existing domain sites remain intact. Back up and validate the Caddy configuration before reload. Restrict certificate directories to root and the caddy group, and repeat group/read permissions after renewal. Use a root-owned timer that runs renewal using both Compose files, validates Caddy, then reloads Caddy; the Nginx renewal script above applies only to the container-proxy topology.
