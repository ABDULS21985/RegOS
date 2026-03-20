#!/usr/bin/env bash
# ============================================================================
#  FC Engine — Health Check & Auto-Recovery
#  Run via cron every 5 minutes: */5 * * * * /opt/fcengine/scripts/healthcheck.sh
# ============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
[[ -f "$SCRIPT_DIR/.env" ]] && source "$SCRIPT_DIR/.env"

API_PORT="${API_PORT:-5100}"
ADMIN_PORT="${ADMIN_PORT:-5200}"
PORTAL_PORT="${PORTAL_PORT:-5300}"
LOG_FILE="${LOG_DIR:-/var/log/fcengine}/healthcheck.log"
MAX_RESTARTS=3
RESTART_WINDOW=3600  # 1 hour — track restarts within this window

log() { echo "[$(date '+%Y-%m-%d %H:%M:%S')] $*" >> "$LOG_FILE"; }

# Track restart count to prevent restart loops
restart_count_file="/tmp/fcengine-restart-counts"
touch "$restart_count_file"

get_restart_count() {
    local svc="$1"
    local cutoff=$(($(date +%s) - RESTART_WINDOW))
    grep "^${svc}:" "$restart_count_file" 2>/dev/null \
        | awk -F: -v cutoff="$cutoff" '$2 > cutoff' \
        | wc -l | tr -d ' '
}

record_restart() {
    local svc="$1"
    echo "${svc}:$(date +%s)" >> "$restart_count_file"
    # Prune old entries
    local cutoff=$(($(date +%s) - RESTART_WINDOW))
    local tmp
    tmp=$(mktemp)
    awk -F: -v cutoff="$cutoff" '$2 > cutoff' "$restart_count_file" > "$tmp"
    mv "$tmp" "$restart_count_file"
}

check_service() {
    local svc_name="$1"
    local port="$2"
    local health_path="${3:-/}"

    # Check systemd service
    if ! systemctl is-active --quiet "$svc_name"; then
        local count
        count=$(get_restart_count "$svc_name")
        if [[ $count -lt $MAX_RESTARTS ]]; then
            log "WARN: ${svc_name} is down — restarting (attempt $((count + 1))/${MAX_RESTARTS})"
            systemctl restart "$svc_name"
            record_restart "$svc_name"
        else
            log "CRIT: ${svc_name} has been restarted ${count} times in the last hour — not restarting"
        fi
        return 1
    fi

    # Check HTTP health
    if ! curl -sf --max-time 10 "http://127.0.0.1:${port}${health_path}" > /dev/null 2>&1; then
        log "WARN: ${svc_name} running but not responding on port ${port}"
        return 1
    fi

    return 0
}

# ── Run checks ─────────────────────────────────────────────────────────────
FAILURES=0

check_service "fcengine-api"    "$API_PORT"    "/health"  || ((FAILURES++))
check_service "fcengine-admin"  "$ADMIN_PORT"  "/"        || ((FAILURES++))
check_service "fcengine-portal" "$PORTAL_PORT" "/"        || ((FAILURES++))

# Check SQL Server
if ! systemctl is-active --quiet mssql-server; then
    log "CRIT: SQL Server is down"
    ((FAILURES++))
fi

# Check disk space (warn at 90%)
DISK_USAGE=$(df / | tail -1 | awk '{print $5}' | tr -d '%')
if [[ $DISK_USAGE -gt 90 ]]; then
    log "WARN: Disk usage at ${DISK_USAGE}%"
    ((FAILURES++))
fi

# Check memory (warn if <256MB free)
FREE_MB=$(free -m | awk '/^Mem:/{print $7}')
if [[ $FREE_MB -lt 256 ]]; then
    log "WARN: Low memory — ${FREE_MB}MB available"
fi

if [[ $FAILURES -eq 0 ]]; then
    # Only log healthy state every hour (not every 5 min)
    MINUTE=$(date +%M)
    if [[ "$MINUTE" -lt 5 ]]; then
        log "OK: All services healthy"
    fi
fi

exit $FAILURES
