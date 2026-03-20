#!/usr/bin/env bash
# ============================================================================
#  FC Engine — Service Management Script
#  Usage: sudo bash manage.sh {status|start|stop|restart|logs|backup|update|rollback}
# ============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="${SCRIPT_DIR}/.env"
[[ -f "$ENV_FILE" ]] && source "$ENV_FILE"

APP_DIR="${APP_DIR:-/opt/fcengine}"
LOG_DIR="${LOG_DIR:-/var/log/fcengine}"
BACKUP_DIR="${BACKUP_DIR:-/opt/fcengine/backups}"
SERVICES=(fcengine-api fcengine-admin fcengine-portal)
ALL_SERVICES=(mssql-server "${SERVICES[@]}")

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
CYAN='\033[0;36m'; NC='\033[0m'; BOLD='\033[1m'

# ── Commands ───────────────────────────────────────────────────────────────

cmd_status() {
    echo -e "\n${BOLD}Service Status${NC}\n"
    printf "  %-25s %-12s %-10s %s\n" "SERVICE" "STATE" "MEMORY" "UPTIME"
    echo "  ─────────────────────────────────────────────────────────────"

    for svc in "${ALL_SERVICES[@]}"; do
        local state
        state=$(systemctl is-active "$svc" 2>/dev/null || echo "inactive")

        local mem="—"
        local uptime="—"
        if [[ "$state" == "active" ]]; then
            mem=$(systemctl show "$svc" --property=MemoryCurrent 2>/dev/null | cut -d= -f2)
            if [[ "$mem" =~ ^[0-9]+$ ]]; then
                mem="$(( mem / 1024 / 1024 ))MB"
            else
                mem="—"
            fi
            uptime=$(systemctl show "$svc" --property=ActiveEnterTimestamp 2>/dev/null | cut -d= -f2 | xargs -I{} date -d "{}" "+%Y-%m-%d %H:%M" 2>/dev/null || echo "—")
        fi

        local color=$RED
        [[ "$state" == "active" ]] && color=$GREEN

        printf "  %-25s ${color}%-12s${NC} %-10s %s\n" "$svc" "$state" "$mem" "$uptime"
    done

    echo ""

    # Disk usage
    echo -e "  ${BOLD}Disk Usage${NC}"
    echo "  ─────────────────────────────────────────────────────────────"
    printf "  %-25s %s\n" "Application" "$(du -sh "$APP_DIR" 2>/dev/null | cut -f1)"
    printf "  %-25s %s\n" "Logs" "$(du -sh "$LOG_DIR" 2>/dev/null | cut -f1)"
    printf "  %-25s %s\n" "Backups" "$(du -sh "$BACKUP_DIR" 2>/dev/null | cut -f1)"
    echo ""
}

cmd_start() {
    local target="${1:-all}"
    if [[ "$target" == "all" ]]; then
        for svc in "${SERVICES[@]}"; do
            systemctl start "$svc"
            echo -e "  ${GREEN}Started${NC} $svc"
        done
    else
        systemctl start "fcengine-${target}"
        echo -e "  ${GREEN}Started${NC} fcengine-${target}"
    fi
}

cmd_stop() {
    local target="${1:-all}"
    if [[ "$target" == "all" ]]; then
        for svc in "${SERVICES[@]}"; do
            systemctl stop "$svc"
            echo -e "  ${YELLOW}Stopped${NC} $svc"
        done
    else
        systemctl stop "fcengine-${target}"
        echo -e "  ${YELLOW}Stopped${NC} fcengine-${target}"
    fi
}

cmd_restart() {
    local target="${1:-all}"
    if [[ "$target" == "all" ]]; then
        echo -e "  ${CYAN}Rolling restart…${NC}"
        for svc in "${SERVICES[@]}"; do
            systemctl restart "$svc"
            echo -e "  ${GREEN}Restarted${NC} $svc"
            sleep 3
        done
    else
        systemctl restart "fcengine-${target}"
        echo -e "  ${GREEN}Restarted${NC} fcengine-${target}"
    fi
}

cmd_logs() {
    local target="${1:-api}"
    local lines="${2:-100}"
    journalctl -u "fcengine-${target}" -n "$lines" --no-pager
}

cmd_follow() {
    local target="${1:-api}"
    journalctl -u "fcengine-${target}" -f
}

cmd_backup() {
    echo -e "  ${CYAN}Running database backup…${NC}"
    bash "$APP_DIR/scripts/backup-cron.sh"
}

cmd_update() {
    local artifact_path="${1:-}"
    [[ -z "$artifact_path" ]] && {
        echo "Usage: manage.sh update <path-to-artifacts>"
        echo "  Artifacts directory should contain: api/ admin/ portal/"
        exit 1
    }

    echo -e "\n  ${BOLD}Zero-Downtime Update${NC}\n"

    # 1. Backup database first
    echo -e "  ${CYAN}Step 1/5: Database backup…${NC}"
    cmd_backup

    # 2. Create rollback snapshot
    local rollback_dir="$APP_DIR/rollback/$(date +%Y%m%d_%H%M%S)"
    echo -e "  ${CYAN}Step 2/5: Creating rollback snapshot…${NC}"
    mkdir -p "$rollback_dir"
    for svc in api admin portal; do
        if [[ -d "$APP_DIR/$svc" ]]; then
            cp -r "$APP_DIR/$svc" "$rollback_dir/$svc"
        fi
    done
    echo -e "  ${GREEN}Snapshot saved: ${rollback_dir}${NC}"

    # 3. Copy new artifacts
    echo -e "  ${CYAN}Step 3/5: Deploying new artifacts…${NC}"
    for svc in api admin portal; do
        if [[ -d "$artifact_path/$svc" ]]; then
            systemctl stop "fcengine-$svc" 2>/dev/null || true
            rm -rf "$APP_DIR/$svc"
            cp -r "$artifact_path/$svc" "$APP_DIR/$svc"
            echo -e "    ${GREEN}Updated${NC} $svc"
        fi
    done

    # 4. Fix permissions
    chown -R "${APP_USER:-fcengine}":"${APP_USER:-fcengine}" "$APP_DIR"

    # 5. Run migrator if present
    if [[ -f "$APP_DIR/migrator/FC.Engine.Migrator.dll" && -d "$artifact_path/migrator" ]]; then
        echo -e "  ${CYAN}Step 4/5: Running migrations…${NC}"
        cp -r "$artifact_path/migrator/"* "$APP_DIR/migrator/"
        sudo -u "${APP_USER:-fcengine}" \
            ConnectionStrings__FcEngine="${CONN_STR:-}" \
            Seeding__AutoSeed=true \
            dotnet "$APP_DIR/migrator/FC.Engine.Migrator.dll" 2>&1 | tail -5
    else
        echo -e "  ${YELLOW}Step 4/5: No migrator found — skipping${NC}"
    fi

    # 6. Rolling restart
    echo -e "  ${CYAN}Step 5/5: Rolling restart…${NC}"
    cmd_restart all

    echo -e "\n  ${GREEN}Update complete.${NC}"
    echo -e "  Rollback available: bash manage.sh rollback ${rollback_dir}\n"
}

cmd_rollback() {
    local rollback_dir="${1:-}"
    [[ -z "$rollback_dir" ]] && {
        echo "Usage: manage.sh rollback <snapshot-path>"
        echo ""
        echo "Available snapshots:"
        ls -1d "$APP_DIR/rollback"/*/ 2>/dev/null | while read -r d; do
            echo "  $d"
        done
        exit 1
    }

    [[ -d "$rollback_dir" ]] || { echo "Snapshot not found: $rollback_dir"; exit 1; }

    echo -e "\n  ${YELLOW}Rolling back to: ${rollback_dir}${NC}\n"

    for svc in api admin portal; do
        if [[ -d "$rollback_dir/$svc" ]]; then
            systemctl stop "fcengine-$svc" 2>/dev/null || true
            rm -rf "$APP_DIR/$svc"
            cp -r "$rollback_dir/$svc" "$APP_DIR/$svc"
            echo -e "  ${GREEN}Restored${NC} $svc"
        fi
    done

    chown -R "${APP_USER:-fcengine}":"${APP_USER:-fcengine}" "$APP_DIR"
    cmd_restart all

    echo -e "\n  ${GREEN}Rollback complete.${NC}\n"
}

# ── Entrypoint ─────────────────────────────────────────────────────────────

case "${1:-help}" in
    status)   cmd_status ;;
    start)    cmd_start "${2:-all}" ;;
    stop)     cmd_stop "${2:-all}" ;;
    restart)  cmd_restart "${2:-all}" ;;
    logs)     cmd_logs "${2:-api}" "${3:-100}" ;;
    follow)   cmd_follow "${2:-api}" ;;
    backup)   cmd_backup ;;
    update)   cmd_update "${2:-}" ;;
    rollback) cmd_rollback "${2:-}" ;;
    *)
        echo ""
        echo "FC Engine — Service Manager"
        echo ""
        echo "Usage: sudo bash manage.sh <command> [target]"
        echo ""
        echo "Commands:"
        echo "  status                     Show all service statuses"
        echo "  start   [api|admin|portal] Start service(s)"
        echo "  stop    [api|admin|portal] Stop service(s)"
        echo "  restart [api|admin|portal] Rolling restart service(s)"
        echo "  logs    [api|admin|portal] [lines]  View recent logs"
        echo "  follow  [api|admin|portal] Tail logs in real-time"
        echo "  backup                     Run database backup now"
        echo "  update  <artifacts-path>   Zero-downtime update with rollback"
        echo "  rollback <snapshot-path>   Rollback to a previous deployment"
        echo ""
        ;;
esac
