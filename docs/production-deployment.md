# Production deployment runbook

This runbook targets an Ubuntu VPS with Docker Engine, the Docker Compose plugin, Git, curl, and an HTTPS reverse proxy or named Cloudflare Tunnel. Run commands as a dedicated deployment user with access to Docker; do not run the application as root.

## 1. Server and secrets

Clone the repository, copy `.env.production.example` to `.env.production`, and replace every placeholder. Restrict it with `chmod 600 .env.production`. Generate independent high-entropy values for PostgreSQL and JWT signing. Never reuse development credentials.

```bash
cp .env.production.example .env.production
chmod 600 .env.production
chmod +x ops/*.sh
./ops/validate-env.sh .env.production
```

Keep ports 3001 and 8080 bound to loopback. Expose only SSH and HTTPS through the VPS firewall. Set `FRONTEND_URL` to the exact public HTTPS origin; paths and trailing slashes are not included.

## 2. HTTPS routing

Route the public hostname to `http://127.0.0.1:3001`. The Next.js server proxies `/api` to the private API container, keeping browser requests same-origin.

Cloudflare Quick Tunnel URLs (`trycloudflare.com`) are temporary demo links and must not be used for production. For Cloudflare, create a named tunnel, bind it to the client hostname, store the tunnel credential outside the repository, and run `cloudflared` as a system service. Alternatively, use Caddy or Nginx with an automatically renewed TLS certificate.

## 3. Deploy

Checkout the reviewed release commit and run:

```bash
ENV_FILE=.env.production ./ops/deploy.sh
```

The script validates secrets and Compose configuration, creates a pre-deployment backup when PostgreSQL is already running, rebuilds images, starts the stack, and waits for API readiness and the web application. A failed health check returns a non-zero exit code and prints recent logs.

Verify the public `/`, `/login`, and `/api/health/ready` routes through HTTPS. Then complete the production validation checklist without running data-creating smoke scripts against a client database.

## 4. Backup and restore drill

Create a backup and copy the resulting `.backup` and `.sha256` files to encrypted off-site storage:

```bash
backup="$(ENV_FILE=.env.production BACKUP_DIR=/var/backups/lankasaas ./ops/backup.sh)"
sha256sum --check "$backup.sha256"
ENV_FILE=.env.production ./ops/verify-restore.sh "$backup"
```

Restore verification creates a separate timestamped database, verifies tables and EF migration history, and removes that database on exit. It never restores over the live database. Configure backup scheduling and retention in the hosting platform only after confirming off-site copies and alerts.

Systemd templates are provided under `ops/systemd`. Review the deployment user and `/opt/lankasaas` paths before installing them, then enable the timer with `systemctl enable --now lankasaas-backup.timer`. The timer creates a daily local backup; configure a separate encrypted off-site transfer and monitor both the timer and transfer. A local file alone is not a disaster-recovery backup.

## 5. Rollback

Application rollback does not automatically reverse database migrations. Migrations must remain backward-compatible with the previous application release. To return application containers to the last recorded healthy commit:

```bash
ENV_FILE=.env.production ./ops/rollback.sh
```

Or provide a reviewed commit explicitly: `./ops/rollback.sh <git-sha>`. The script refuses to proceed with tracked working-tree changes. If a migration is not backward-compatible, stop and follow a reviewed database recovery plan rather than improvising a downgrade.

## 6. Monitoring and incident response

Monitor public HTTPS availability, `/api/health`, and `/api/health/ready`. Alert on repeated 5xx responses, readiness failures, disk pressure, container restarts, failed backups, and certificate or tunnel expiry. Preserve application and proxy logs with restricted access and defined retention.

For an incident: record the time and release SHA, stop further deployments, capture logs, assess tenant-data exposure, restore service with the least destructive action, and notify affected clients when required. Never delete volumes or restore over production until a verified backup and explicit recovery decision exist.
