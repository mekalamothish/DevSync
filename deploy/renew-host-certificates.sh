#!/usr/bin/env sh
set -eu
cd "$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
docker compose --env-file .env -f compose.production.yaml -f compose.host-proxy.yaml run --rm --no-deps certificate renew --quiet
chgrp -R caddy /etc/devsync/tls
find /etc/devsync/tls -type d -exec chmod 750 {} +
find /etc/devsync/tls -type f -exec chmod 640 {} +
caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile
systemctl reload caddy
