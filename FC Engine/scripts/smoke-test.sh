#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════
# FC Engine — Endpoint Smoke Test Suite
# Tests all public HTTP endpoints for reachability and shape.
# Usage:  ./scripts/smoke-test.sh [API_BASE] [ADMIN_BASE] [PORTAL_BASE]
# Defaults: http://localhost:5100  http://localhost:5200  http://localhost:5300
# ═══════════════════════════════════════════════════════════════
set -euo pipefail

API="${1:-http://localhost:5100}"
ADMIN="${2:-http://localhost:5200}"
PORTAL="${3:-http://localhost:5300}"

PASS=0
FAIL=0
SKIP=0
FAILURES=""

green()  { printf "\033[32m%s\033[0m" "$1"; }
red()    { printf "\033[31m%s\033[0m" "$1"; }
yellow() { printf "\033[33m%s\033[0m" "$1"; }

# ── helpers ──────────────────────────────────────────────────────
check_http() {
    local label="$1"
    local method="$2"
    local url="$3"
    local expect_status="${4:-200}"
    shift 4
    local extra_args=("$@")

    local status
    status=$(curl -s -o /dev/null -w "%{http_code}" -X "$method" "${extra_args[@]}" "$url" 2>/dev/null || echo "000")

    if [[ "$status" == "$expect_status" ]]; then
        printf "  %-55s %s\n" "$label" "$(green "✓ $status")"
        ((PASS++))
    elif [[ "$status" == "000" ]]; then
        printf "  %-55s %s\n" "$label" "$(yellow "⊘ unreachable")"
        ((SKIP++))
    else
        printf "  %-55s %s (expected %s)\n" "$label" "$(red "✗ $status")" "$expect_status"
        ((FAIL++))
        FAILURES="${FAILURES}\n  ✗ ${label}: got ${status}, expected ${expect_status}"
    fi
}

check_json() {
    local label="$1"
    local method="$2"
    local url="$3"
    local expect_status="${4:-200}"
    shift 4
    local extra_args=("$@")

    local response
    response=$(curl -s -w "\n%{http_code}" -X "$method" -H "Content-Type: application/json" "${extra_args[@]}" "$url" 2>/dev/null || echo -e "\n000")

    local status
    status=$(echo "$response" | tail -1)
    local body
    body=$(echo "$response" | sed '$d')

    if [[ "$status" == "$expect_status" ]]; then
        # Verify it is valid JSON (unless empty or 204)
        if [[ "$status" != "204" && -n "$body" ]]; then
            if echo "$body" | python3 -m json.tool >/dev/null 2>&1; then
                printf "  %-55s %s\n" "$label" "$(green "✓ $status (json)")"
            else
                printf "  %-55s %s\n" "$label" "$(yellow "✓ $status (non-json)")"
            fi
        else
            printf "  %-55s %s\n" "$label" "$(green "✓ $status")"
        fi
        ((PASS++))
    elif [[ "$status" == "000" ]]; then
        printf "  %-55s %s\n" "$label" "$(yellow "⊘ unreachable")"
        ((SKIP++))
    else
        printf "  %-55s %s (expected %s)\n" "$label" "$(red "✗ $status")" "$expect_status"
        ((FAIL++))
        FAILURES="${FAILURES}\n  ✗ ${label}: got ${status}, expected ${expect_status}"
    fi
}

# ═══════════════════════════════════════════════════════════════
echo ""
echo "═══════════════════════════════════════════════════════════"
echo " FC Engine Smoke Tests"
echo " API:    $API"
echo " Admin:  $ADMIN"
echo " Portal: $PORTAL"
echo "═══════════════════════════════════════════════════════════"
echo ""

# ── 1. Health & Infrastructure ──────────────────────────────────
echo "▸ Health & Infrastructure"
check_json "GET /health"                GET  "$API/health"            200
check_http "GET /health/live"           GET  "$API/health/live"       200
check_json "GET /health/ready"          GET  "$API/health/ready"      200
check_http "GET /metrics"               GET  "$API/metrics"           200
echo ""

# ── 2. Auth (anonymous endpoints) ───────────────────────────────
echo "▸ Auth — Anonymous"
check_json "POST /auth/login (no body → 400)"     POST "$API/api/v1/auth/login"    400 -d '{}'
check_json "POST /auth/login (bad creds → 401)"   POST "$API/api/v1/auth/login"    401 \
    -d '{"email":"bad@example.com","password":"wrong"}'
check_json "POST /auth/refresh (empty → 400)"     POST "$API/api/v1/auth/refresh"  400 -d '{}'
check_json "POST /auth/revoke (unauth → 401)"     POST "$API/api/v1/auth/revoke"   401 -d '{"refreshToken":"x"}'
echo ""

# ── 3. Protected endpoints (expect 401 without token) ──────────
echo "▸ Protected Endpoints — Auth Required (expect 401)"
check_http "GET /submissions/1"                     GET  "$API/api/v1/submissions/1"                     401
check_http "GET /submissions/institution/1"         GET  "$API/api/v1/submissions/institution/1"         401
check_http "GET /templates/"                        GET  "$API/api/v1/templates/"                        401
check_http "GET /templates/MFCR_300"                GET  "$API/api/v1/templates/MFCR_300"                401
check_http "POST /templates/"                       POST "$API/api/v1/templates/"                        401
check_http "GET /schemas/MFCR_300/xsd"              GET  "$API/api/v1/schemas/MFCR_300/xsd"              401
check_http "GET /schemas/published"                 GET  "$API/api/v1/schemas/published"                 401
check_http "POST /schemas/seed"                     POST "$API/api/v1/schemas/seed"                      401
check_http "POST /schemas/seed-formulas"            POST "$API/api/v1/schemas/seed-formulas"             401
check_http "GET /filing-calendar/rag"               GET  "$API/api/v1/filing-calendar/rag"               401
check_http "POST /filing-calendar/deadline-override" POST "$API/api/v1/filing-calendar/deadline-override" 401
check_http "GET /privacy/dsar"                      GET  "$API/api/v1/privacy/dsar"                      401
check_http "POST /privacy/dsar"                     POST "$API/api/v1/privacy/dsar"                      401
check_http "GET /webhooks/"                         GET  "$API/api/v1/webhooks/"                         401
check_http "POST /webhooks/"                        POST "$API/api/v1/webhooks/"                         401
check_http "GET /migration/jobs"                    GET  "$API/api/v1/migration/jobs"                    401
check_http "POST /migration/jobs/upload"            POST "$API/api/v1/migration/jobs/upload"             401
check_http "GET /returns/X/mappings/test"            GET  "$API/api/v1/returns/X/mappings/test"           401
echo ""

# ── 4. v2 endpoints ────────────────────────────────────────────
echo "▸ v2 Endpoints — Auth Required (expect 401)"
check_http "GET  /v2/submissions/1"                 GET  "$API/api/v2/submissions/1"                     401
check_http "GET  /v2/schemas/published"             GET  "$API/api/v2/schemas/published"                 401
echo ""

# ── 5. Admin Portal ────────────────────────────────────────────
echo "▸ Admin Portal"
check_http "GET / (Admin home → 200 or 302)"       GET  "$ADMIN/"                     200
check_http "GET /account/login"                     GET  "$ADMIN/account/login"         200
echo ""

# ── 6. Portal ──────────────────────────────────────────────────
echo "▸ Portal"
check_http "GET / (Portal home → 200 or 302)"      GET  "$PORTAL/"                     200
check_http "GET /login"                             GET  "$PORTAL/login"                200
echo ""

# ── Summary ─────────────────────────────────────────────────────
echo "═══════════════════════════════════════════════════════════"
printf " Results: $(green "$PASS passed")  "
if [[ $FAIL -gt 0 ]]; then
    printf "$(red "$FAIL failed")  "
else
    printf "$FAIL failed  "
fi
if [[ $SKIP -gt 0 ]]; then
    printf "$(yellow "$SKIP unreachable")"
fi
echo ""

if [[ -n "$FAILURES" ]]; then
    echo ""
    echo " Failures:"
    echo -e "$FAILURES"
fi

echo "═══════════════════════════════════════════════════════════"
echo ""

if [[ $FAIL -gt 0 ]]; then
    exit 1
fi
exit 0
