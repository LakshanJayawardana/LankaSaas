#!/usr/bin/env bash
set -euo pipefail

ENV_FILE="${1:-.env.production}"
[[ -f "$ENV_FILE" ]] || { echo "Missing $ENV_FILE" >&2; exit 1; }
set -a; source "$ENV_FILE"; set +a

required=(POSTGRES_DB POSTGRES_USER POSTGRES_PASSWORD JWT_KEY FRONTEND_URL)
for name in "${required[@]}"; do [[ -n "${!name:-}" ]] || { echo "$name is required" >&2; exit 1; }; done
[[ ${#JWT_KEY} -ge 32 ]] || { echo "JWT_KEY must contain at least 32 characters" >&2; exit 1; }
[[ "$FRONTEND_URL" == https://* ]] || { echo "FRONTEND_URL must use HTTPS" >&2; exit 1; }
[[ "$POSTGRES_PASSWORD" != "change-me" && "$JWT_KEY" != *"replace-with"* ]] || { echo "Placeholder secrets are not allowed" >&2; exit 1; }
[[ "${NEXT_PUBLIC_API_URL:-/api}" == "/api" ]] || { echo "NEXT_PUBLIC_API_URL must be /api for the same-origin production proxy" >&2; exit 1; }
[[ "${PAYHERE_SANDBOX:-false}" =~ ^(true|false)$ ]] || { echo "PAYHERE_SANDBOX must be true or false" >&2; exit 1; }
echo "Production environment validation passed."
