#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)";cd "$ROOT";ENV_FILE="${ENV_FILE:-.env.production}";TARGET="${1:-$(cat .deployments/previous-sha 2>/dev/null || true)}"
[[ -n "$TARGET" ]] || { echo "Usage: $0 <known-good-git-sha>" >&2;exit 1; };git cat-file -e "$TARGET^{commit}" 2>/dev/null||{ echo "Unknown commit $TARGET" >&2;exit 1; }
[[ -z "$(git status --porcelain --untracked-files=no)" ]]||{ echo "Tracked working tree changes must be committed or removed before rollback" >&2;exit 1; }
echo "Rollback changes application containers only. Database migrations are not automatically reversed."
git switch --detach "$TARGET";"$ROOT/ops/deploy.sh"
