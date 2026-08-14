#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)";cd "$ROOT";ENV_FILE="${ENV_FILE:-.env.production}"
"$ROOT/ops/validate-env.sh" "$ENV_FILE";mkdir -p .deployments
target="$(git rev-parse HEAD)";previous="$(cat .deployments/current-sha 2>/dev/null || echo "$target")";echo "$previous" > .deployments/previous-sha
if docker compose --env-file "$ENV_FILE" -f docker-compose.yml -f docker-compose.production.yml ps db --status running --quiet|grep -q .;then backup="$($ROOT/ops/backup.sh)";echo "Pre-deployment backup: $backup";fi
compose=(docker compose --env-file "$ENV_FILE" -f docker-compose.yml -f docker-compose.production.yml)
"${compose[@]}" config --quiet;"${compose[@]}" build --pull;"${compose[@]}" up -d --remove-orphans
for attempt in {1..30};do if curl --fail --silent http://127.0.0.1:${API_PORT:-8080}/health/ready >/dev/null&&curl --fail --silent http://127.0.0.1:${WEB_PORT:-3001}/ >/dev/null;then echo "$target" > .deployments/current-sha;echo "Deployment healthy at commit $target.";exit 0;fi;sleep 2;done
"${compose[@]}" logs --tail=100 api web >&2;echo "Deployment health check failed. Previous commit: $previous" >&2;exit 1
