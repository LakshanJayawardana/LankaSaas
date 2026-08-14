#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)";cd "$ROOT"
ENV_FILE="${ENV_FILE:-.env.production}";BACKUP_DIR="${BACKUP_DIR:-$ROOT/backups}"
"$ROOT/ops/validate-env.sh" "$ENV_FILE" >/dev/null
case "$BACKUP_DIR" in /|"" ) echo "Unsafe BACKUP_DIR" >&2;exit 1;; esac
mkdir -p "$BACKUP_DIR";chmod 700 "$BACKUP_DIR"
set -a;source "$ENV_FILE";set +a
stamp="$(date -u +%Y%m%dT%H%M%SZ)";file="$BACKUP_DIR/lankasaas-$stamp.backup";tmp="$file.partial"
compose=(docker compose --env-file "$ENV_FILE" -f docker-compose.yml -f docker-compose.production.yml)
"${compose[@]}" exec -T db pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc > "$tmp"
[[ -s "$tmp" ]] || { rm -f "$tmp";echo "Backup is empty" >&2;exit 1; }
mv "$tmp" "$file";chmod 600 "$file";sha256sum "$file" > "$file.sha256"
echo "$file"
