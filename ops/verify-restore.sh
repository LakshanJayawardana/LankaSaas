#!/usr/bin/env bash
set -euo pipefail

[[ $# -eq 1 ]] || { echo "Usage: $0 /absolute/path/file.backup" >&2;exit 1; }
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)";cd "$ROOT";ENV_FILE="${ENV_FILE:-.env.production}";BACKUP_FILE="$(realpath "$1")"
[[ -f "$BACKUP_FILE" && -s "$BACKUP_FILE" ]] || { echo "Backup file is missing or empty" >&2;exit 1; }
"$ROOT/ops/validate-env.sh" "$ENV_FILE" >/dev/null;set -a;source "$ENV_FILE";set +a
verify_db="lankasaas_restore_verify_$(date -u +%Y%m%d%H%M%S)";[[ "$verify_db" == lankasaas_restore_verify_* ]] || exit 1
compose=(docker compose --env-file "$ENV_FILE" -f docker-compose.yml -f docker-compose.production.yml)
cleanup(){ "${compose[@]}" exec -T db dropdb -U "$POSTGRES_USER" --if-exists "$verify_db" >/dev/null 2>&1 || true; };trap cleanup EXIT
"${compose[@]}" exec -T db createdb -U "$POSTGRES_USER" "$verify_db"
cat "$BACKUP_FILE"|"${compose[@]}" exec -T db pg_restore -U "$POSTGRES_USER" -d "$verify_db" --exit-on-error
table_count="$("${compose[@]}" exec -T db psql -U "$POSTGRES_USER" -d "$verify_db" -Atc "SELECT count(*) FROM information_schema.tables WHERE table_schema='public';"|tr -d '\r')"
[[ "$table_count" =~ ^[0-9]+$ && "$table_count" -gt 0 ]] || { echo "Restored database contains no public tables" >&2;exit 1; }
"${compose[@]}" exec -T db psql -U "$POSTGRES_USER" -d "$verify_db" -v ON_ERROR_STOP=1 -Atc 'SELECT count(*) FROM "__EFMigrationsHistory";' >/dev/null
echo "Restore verification passed in isolated database $verify_db ($table_count public tables)."
