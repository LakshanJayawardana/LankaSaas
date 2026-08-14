#!/usr/bin/env bash
set -euo pipefail

BACKUP_DIR="${BACKUP_DIR:-/var/backups/lankasaas}";MAX_BACKUP_AGE_HOURS="${MAX_BACKUP_AGE_HOURS:-30}"
[[ "$MAX_BACKUP_AGE_HOURS" =~ ^[0-9]+$ && "$MAX_BACKUP_AGE_HOURS" -ge 1 ]] || { echo "MAX_BACKUP_AGE_HOURS must be a positive integer" >&2;exit 1; }
[[ -d "$BACKUP_DIR" ]] || { echo "Backup directory does not exist: $BACKUP_DIR" >&2;exit 1; }
latest="$(find "$BACKUP_DIR" -maxdepth 1 -type f -name 'lankasaas-*.backup' -printf '%T@ %p\n'|sort -nr|head -1|cut -d' ' -f2-)"
[[ -n "$latest" && -s "$latest" ]] || { echo "No usable LankaSaaS backup found" >&2;exit 1; }
[[ -f "$latest.sha256" ]] || { echo "Checksum is missing for $latest" >&2;exit 1; }
(cd "$(dirname "$latest")"&&sha256sum --check "$(basename "$latest").sha256") >/dev/null
age_seconds="$(( $(date -u +%s) - $(stat -c %Y "$latest") ))";max_seconds="$((MAX_BACKUP_AGE_HOURS*3600))"
[[ "$age_seconds" -le "$max_seconds" ]] || { echo "Latest backup is too old: $((age_seconds/3600)) hours" >&2;exit 1; }
echo "Backup healthy: $latest ($((age_seconds/60)) minutes old)."
