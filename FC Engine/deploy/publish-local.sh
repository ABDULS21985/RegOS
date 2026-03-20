#!/usr/bin/env bash
# ============================================================================
#  FC Engine — Local Build & Publish Script
#  Builds all projects and creates a deployable artifact package.
#
#  Usage:
#    bash deploy/publish-local.sh                    # Build + package
#    bash deploy/publish-local.sh --upload user@host # Build + upload to VPS
# ============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SLN_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PUBLISH_DIR="${SLN_DIR}/publish"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
ARCHIVE_NAME="fcengine-${TIMESTAMP}.tar.gz"

RED='\033[0;31m'; GREEN='\033[0;32m'; CYAN='\033[0;36m'
NC='\033[0m'; BOLD='\033[1m'

info()    { echo -e "${CYAN}[INFO]${NC}  $*"; }
success() { echo -e "${GREEN}[OK]${NC}    $*"; }

# ── Validate solution ─────────────────────────────────────────────────────
[[ -f "$SLN_DIR/FCEngine.sln" ]] || { echo "Solution not found at $SLN_DIR"; exit 1; }

# ── Clean previous publish ────────────────────────────────────────────────
rm -rf "$PUBLISH_DIR"
mkdir -p "$PUBLISH_DIR"

# ── Restore packages ──────────────────────────────────────────────────────
info "Restoring NuGet packages…"
dotnet restore "$SLN_DIR/FCEngine.sln" --verbosity quiet

# ── Publish all projects ──────────────────────────────────────────────────
publish() {
    local name="$1" project="$2" output="$3"
    info "Publishing ${name}…"
    dotnet publish "$project" \
        -c Release \
        -o "$output" \
        --no-restore \
        --no-self-contained \
        /p:DebugType=none \
        /p:DebugSymbols=false \
        --verbosity quiet
    success "${name} → $(basename "$output")/"
}

publish "API"      "$SLN_DIR/src/FC.Engine.Api/FC.Engine.Api.csproj"          "$PUBLISH_DIR/api"
publish "Admin"    "$SLN_DIR/src/FC.Engine.Admin/FC.Engine.Admin.csproj"      "$PUBLISH_DIR/admin"
publish "Portal"   "$SLN_DIR/src/FC.Engine.Portal/FC.Engine.Portal.csproj"    "$PUBLISH_DIR/portal"
publish "Migrator" "$SLN_DIR/src/FC.Engine.Migrator/FC.Engine.Migrator.csproj" "$PUBLISH_DIR/migrator"

# ── Copy supporting files ─────────────────────────────────────────────────
info "Copying schema and scripts…"
cp -f "$SLN_DIR/schema.sql" "$PUBLISH_DIR/" 2>/dev/null || true
cp -rf "$SLN_DIR/scripts" "$PUBLISH_DIR/" 2>/dev/null || true

# ── Copy deploy scripts ──────────────────────────────────────────────────
cp -f "$SCRIPT_DIR/deploy.sh" "$PUBLISH_DIR/"
cp -f "$SCRIPT_DIR/manage.sh" "$PUBLISH_DIR/"
cp -f "$SCRIPT_DIR/.env.example" "$PUBLISH_DIR/"

# ── Create archive ────────────────────────────────────────────────────────
info "Creating deployment archive…"
tar -czf "$SLN_DIR/$ARCHIVE_NAME" -C "$PUBLISH_DIR" .

ARCHIVE_SIZE=$(du -sh "$SLN_DIR/$ARCHIVE_NAME" | cut -f1)
success "Archive: ${ARCHIVE_NAME} (${ARCHIVE_SIZE})"

# ── Upload if requested ──────────────────────────────────────────────────
if [[ "${1:-}" == "--upload" ]]; then
    VPS_HOST="${2:?Usage: --upload user@host}"
    VPS_PATH="${3:-/tmp/fcengine-deploy}"

    info "Uploading to ${VPS_HOST}:${VPS_PATH}…"
    ssh "$VPS_HOST" "mkdir -p $VPS_PATH"
    scp "$SLN_DIR/$ARCHIVE_NAME" "$VPS_HOST:$VPS_PATH/"

    success "Uploaded. To deploy on VPS, run:"
    echo ""
    echo "  ssh $VPS_HOST"
    echo "  cd $VPS_PATH"
    echo "  tar -xzf $ARCHIVE_NAME"
    echo "  cp .env.example .env && nano .env   # Edit configuration"
    echo "  sudo bash deploy.sh                 # First-time setup"
    echo "  # — or for updates —"
    echo "  sudo bash manage.sh update $VPS_PATH"
    echo ""
fi

# ── Summary ───────────────────────────────────────────────────────────────
echo ""
echo -e "${BOLD}Build Summary${NC}"
echo "  ─────────────────────────────────────────"
for svc in api admin portal migrator; do
    if [[ -d "$PUBLISH_DIR/$svc" ]]; then
        count=$(find "$PUBLISH_DIR/$svc" -type f | wc -l | tr -d ' ')
        size=$(du -sh "$PUBLISH_DIR/$svc" | cut -f1)
        printf "  %-12s %6s files   %s\n" "$svc" "$count" "$size"
    fi
done
echo "  ─────────────────────────────────────────"
echo -e "  Archive:     ${BOLD}$SLN_DIR/$ARCHIVE_NAME${NC}"
echo ""
echo -e "  ${BOLD}Deploy to VPS:${NC}"
echo "    bash deploy/publish-local.sh --upload root@your-vps-ip"
echo ""
