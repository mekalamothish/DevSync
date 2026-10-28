#!/usr/bin/env sh
set -eu
cd "$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
docker compose --env-file .env -f compose.production.yaml run --rm --no-deps certificate renew --quiet
docker compose --env-file .env -f compose.production.yaml exec -T proxy nginx -t
docker compose --env-file .env -f compose.production.yaml exec -T proxy nginx -s reload
