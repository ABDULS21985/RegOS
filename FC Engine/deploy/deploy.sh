#!/usr/bin/env bash
# ============================================================================
#  FC Engine — Full VPS Deployment Script
#  Deploys API + Admin Portal + Institution Portal + SQL Server + Nginx + SSL
#
#  Target: Ubuntu 22.04/24.04 LTS VPS (Debian-based)
#  Stack:  .NET 10, SQL Server 2022, Nginx, Certbot (Let's Encrypt)
#
#  Usage:
#    1. Copy this entire deploy/ folder to your VPS
#    2. Edit deploy/.env with your production values
#    3. Run: sudo bash deploy.sh
#
#  Re-running is safe — the script is idempotent.
# ============================================================================
set -euo pipefail
IFS=$'\n\t'

# ── Colour helpers ──────────────────────────────────────────────────────────
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
CYAN='\033[0;36m'; NC='\033[0m'; BOLD='\033[1m'

info()    { echo -e "${CYAN}[INFO]${NC}  $*"; }
success() { echo -e "${GREEN}[OK]${NC}    $*"; }
warn()    { echo -e "${YELLOW}[WARN]${NC}  $*"; }
err()     { echo -e "${RED}[ERR]${NC}   $*" >&2; }
fatal()   { err "$*"; exit 1; }
banner()  { echo -e "\n${BOLD}━━━ $* ━━━${NC}\n"; }

# ── Pre-flight checks ──────────────────────────────────────────────────────
[[ $EUID -eq 0 ]] || fatal "Run this script as root: sudo bash deploy.sh"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="${SCRIPT_DIR}/.env"

[[ -f "$ENV_FILE" ]] || fatal "Missing ${ENV_FILE}. Copy .env.example → .env and fill in values."

# shellcheck source=/dev/null
source "$ENV_FILE"

# ── Required .env variables ────────────────────────────────────────────────
: "${DOMAIN_ADMIN:?Set DOMAIN_ADMIN in .env (e.g. admin.regos.app)}"
: "${DOMAIN_PORTAL:?Set DOMAIN_PORTAL in .env (e.g. portal.regos.app)}"
: "${DOMAIN_API:?Set DOMAIN_API in .env (e.g. api.regos.app)}"
: "${DB_PASSWORD:?Set DB_PASSWORD in .env}"
: "${ADMIN_PASSWORD:?Set ADMIN_PASSWORD in .env}"
: "${SSL_EMAIL:?Set SSL_EMAIL in .env (for Let's Encrypt)}"

# ── Defaults ───────────────────────────────────────────────────────────────
APP_USER="${APP_USER:-fcengine}"
APP_DIR="${APP_DIR:-/opt/fcengine}"
DOTNET_VERSION="${DOTNET_VERSION:-10.0}"
SQL_PORT="${SQL_PORT:-1433}"
API_PORT="${API_PORT:-5100}"
ADMIN_PORT="${ADMIN_PORT:-5200}"
PORTAL_PORT="${PORTAL_PORT:-5300}"
DB_NAME="${DB_NAME:-FcEngine}"
ENABLE_SSL="${ENABLE_SSL:-true}"
BACKUP_DIR="${BACKUP_DIR:-/opt/fcengine/backups}"
LOG_DIR="${LOG_DIR:-/var/log/fcengine}"
SRC_DIR="${SRC_DIR:-}"  # Path to solution source (if building on server)
DEPLOY_ARTIFACTS="${DEPLOY_ARTIFACTS:-}"  # Path to pre-built publish artifacts

# ── Connection string (used by all services) ───────────────────────────────
CONN_STR="Server=127.0.0.1,${SQL_PORT};Database=${DB_NAME};User Id=sa;Password=${DB_PASSWORD};TrustServerCertificate=true;MultipleActiveResultSets=true"

# ============================================================================
#  PHASE 1 — System Preparation
# ============================================================================
banner "Phase 1: System Preparation"

info "Updating system packages…"
export DEBIAN_FRONTEND=noninteractive
apt-get update -qq
apt-get upgrade -y -qq

info "Installing base dependencies…"
apt-get install -y -qq \
    curl wget gnupg2 apt-transport-https software-properties-common \
    unzip jq ufw fail2ban logrotate ca-certificates lsb-release \
    > /dev/null

success "System packages updated"

# ── Create application user ────────────────────────────────────────────────
if ! id "$APP_USER" &>/dev/null; then
    useradd --system --shell /usr/sbin/nologin --home-dir "$APP_DIR" "$APP_USER"
    success "Created system user: $APP_USER"
else
    success "User $APP_USER already exists"
fi

# ── Create directory structure ─────────────────────────────────────────────
info "Creating directory structure…"
mkdir -p "$APP_DIR"/{api,admin,portal,migrator,keys,scripts,schema}
mkdir -p "$LOG_DIR"/{api,admin,portal}
mkdir -p "$BACKUP_DIR"
mkdir -p "$APP_DIR/uploads"

chown -R "$APP_USER":"$APP_USER" "$APP_DIR"
chown -R "$APP_USER":"$APP_USER" "$LOG_DIR"
chmod 700 "$APP_DIR/keys"

success "Directories created at $APP_DIR"

# ============================================================================
#  PHASE 2 — Install .NET Runtime
# ============================================================================
banner "Phase 2: .NET ${DOTNET_VERSION} Runtime"

if dotnet --list-runtimes 2>/dev/null | grep -q "Microsoft.AspNetCore.App ${DOTNET_VERSION}"; then
    success ".NET ${DOTNET_VERSION} ASP.NET runtime already installed"
else
    info "Installing .NET ${DOTNET_VERSION} runtime…"

    # Microsoft package feed
    wget -q "https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb" \
        -O /tmp/packages-microsoft-prod.deb
    dpkg -i /tmp/packages-microsoft-prod.deb > /dev/null
    rm -f /tmp/packages-microsoft-prod.deb

    apt-get update -qq
    apt-get install -y -qq "aspnetcore-runtime-${DOTNET_VERSION}" > /dev/null 2>&1 || {
        warn ".NET ${DOTNET_VERSION} not in package feed yet — installing via dotnet-install script"
        curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- \
            --channel "$DOTNET_VERSION" --runtime aspnetcore --install-dir /usr/share/dotnet
        ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet
    }

    success ".NET $(dotnet --version 2>/dev/null || echo "$DOTNET_VERSION") installed"
fi

# If building on server, also install SDK
if [[ -n "$SRC_DIR" ]]; then
    if ! dotnet --list-sdks 2>/dev/null | grep -q "${DOTNET_VERSION}"; then
        info "Installing .NET SDK for on-server build…"
        apt-get install -y -qq "dotnet-sdk-${DOTNET_VERSION}" > /dev/null 2>&1 || {
            curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- \
                --channel "$DOTNET_VERSION" --install-dir /usr/share/dotnet
        }
        success ".NET SDK installed"
    fi
fi

# ============================================================================
#  PHASE 3 — Install & Configure SQL Server 2022
# ============================================================================
banner "Phase 3: SQL Server 2022"

if systemctl is-active --quiet mssql-server 2>/dev/null; then
    success "SQL Server already running"
else
    info "Installing SQL Server 2022…"

    curl -fsSL https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor -o /usr/share/keyrings/microsoft-prod.gpg 2>/dev/null
    echo "deb [arch=amd64 signed-by=/usr/share/keyrings/microsoft-prod.gpg] https://packages.microsoft.com/ubuntu/$(lsb_release -rs)/mssql-server-2022 $(lsb_release -cs) main" \
        > /etc/apt/sources.list.d/mssql-server-2022.list

    apt-get update -qq
    apt-get install -y -qq mssql-server > /dev/null

    # Configure SQL Server
    MSSQL_SA_PASSWORD="$DB_PASSWORD" \
    MSSQL_PID="Developer" \
    /opt/mssql/bin/mssql-conf -n setup accept-eula

    # Install CLI tools
    curl -fsSL https://packages.microsoft.com/keys/microsoft.asc | apt-key add - 2>/dev/null
    echo "deb [arch=amd64] https://packages.microsoft.com/ubuntu/$(lsb_release -rs)/prod $(lsb_release -cs) main" \
        > /etc/apt/sources.list.d/mssql-tools.list
    ACCEPT_EULA=Y apt-get install -y -qq mssql-tools18 unixodbc-dev > /dev/null 2>&1 || true

    # Add sqlcmd to PATH
    if ! grep -q '/opt/mssql-tools18/bin' /etc/environment 2>/dev/null; then
        echo 'PATH="/opt/mssql-tools18/bin:$PATH"' >> /etc/environment
    fi
    export PATH="/opt/mssql-tools18/bin:$PATH"

    systemctl enable mssql-server
    systemctl start mssql-server

    success "SQL Server 2022 installed and running"
fi

# ── Wait for SQL Server readiness ──────────────────────────────────────────
info "Waiting for SQL Server to accept connections…"
for i in $(seq 1 30); do
    if /opt/mssql-tools18/bin/sqlcmd -S "127.0.0.1,${SQL_PORT}" -U sa -P "$DB_PASSWORD" \
        -C -Q "SELECT 1" &>/dev/null; then
        success "SQL Server is ready"
        break
    fi
    [[ $i -eq 30 ]] && fatal "SQL Server failed to start within 30 seconds"
    sleep 1
done

# ── Create database if not exists ──────────────────────────────────────────
info "Ensuring database '${DB_NAME}' exists…"
/opt/mssql-tools18/bin/sqlcmd -S "127.0.0.1,${SQL_PORT}" -U sa -P "$DB_PASSWORD" -C \
    -Q "IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'${DB_NAME}') CREATE DATABASE [${DB_NAME}];" \
    2>/dev/null
success "Database ready"

# ============================================================================
#  PHASE 4 — Build or Deploy Application Artifacts
# ============================================================================
banner "Phase 4: Application Artifacts"

publish_project() {
    local project_name="$1"
    local project_path="$2"
    local output_dir="$3"

    info "Publishing ${project_name}…"
    dotnet publish "$project_path" \
        -c Release \
        -o "$output_dir" \
        --no-self-contained \
        -r linux-x64 \
        /p:DebugType=none \
        /p:DebugSymbols=false \
        > /dev/null 2>&1

    success "${project_name} published to ${output_dir}"
}

if [[ -n "$SRC_DIR" ]]; then
    # ── Build from source on server ────────────────────────────────────
    info "Building from source: $SRC_DIR"

    [[ -f "$SRC_DIR/FCEngine.sln" ]] || fatal "Solution not found at $SRC_DIR/FCEngine.sln"

    info "Restoring NuGet packages…"
    dotnet restore "$SRC_DIR/FCEngine.sln" --verbosity quiet > /dev/null

    publish_project "API"      "$SRC_DIR/src/FC.Engine.Api/FC.Engine.Api.csproj"            "$APP_DIR/api"
    publish_project "Admin"    "$SRC_DIR/src/FC.Engine.Admin/FC.Engine.Admin.csproj"          "$APP_DIR/admin"
    publish_project "Portal"   "$SRC_DIR/src/FC.Engine.Portal/FC.Engine.Portal.csproj"        "$APP_DIR/portal"
    publish_project "Migrator" "$SRC_DIR/src/FC.Engine.Migrator/FC.Engine.Migrator.csproj"    "$APP_DIR/migrator"

    # Copy schema and seed scripts
    cp -f "$SRC_DIR/schema.sql" "$APP_DIR/schema/" 2>/dev/null || true
    cp -rf "$SRC_DIR/scripts/"* "$APP_DIR/scripts/" 2>/dev/null || true

elif [[ -n "$DEPLOY_ARTIFACTS" ]]; then
    # ── Copy pre-built artifacts ───────────────────────────────────────
    info "Deploying pre-built artifacts from: $DEPLOY_ARTIFACTS"

    for svc in api admin portal migrator; do
        if [[ -d "$DEPLOY_ARTIFACTS/$svc" ]]; then
            cp -rf "$DEPLOY_ARTIFACTS/$svc/"* "$APP_DIR/$svc/"
            success "Copied $svc artifacts"
        else
            warn "No artifacts found for $svc at $DEPLOY_ARTIFACTS/$svc"
        fi
    done

    # Copy schema and seed scripts
    cp -f "$DEPLOY_ARTIFACTS/schema.sql" "$APP_DIR/schema/" 2>/dev/null || true
    cp -rf "$DEPLOY_ARTIFACTS/scripts/"* "$APP_DIR/scripts/" 2>/dev/null || true

else
    # ── No source or artifacts — check if already deployed ─────────────
    if [[ -f "$APP_DIR/api/FC.Engine.Api.dll" ]]; then
        warn "No SRC_DIR or DEPLOY_ARTIFACTS set — using existing deployment at $APP_DIR"
    else
        fatal "Set SRC_DIR (build on server) or DEPLOY_ARTIFACTS (pre-built) in .env"
    fi
fi

chown -R "$APP_USER":"$APP_USER" "$APP_DIR"

# ============================================================================
#  PHASE 5 — Generate JWT Signing Keys
# ============================================================================
banner "Phase 5: JWT Signing Keys"

JWT_KEY_PATH="$APP_DIR/keys/jwt-private-key.pem"
JWT_PUB_PATH="$APP_DIR/keys/jwt-public-key.pem"

if [[ -f "$JWT_KEY_PATH" ]]; then
    success "JWT signing key already exists"
else
    info "Generating RSA-2048 JWT signing key pair…"
    openssl genrsa -out "$JWT_KEY_PATH" 2048 2>/dev/null
    openssl rsa -in "$JWT_KEY_PATH" -pubout -out "$JWT_PUB_PATH" 2>/dev/null
    chmod 600 "$JWT_KEY_PATH"
    chmod 644 "$JWT_PUB_PATH"
    chown "$APP_USER":"$APP_USER" "$JWT_KEY_PATH" "$JWT_PUB_PATH"
    success "JWT key pair generated"
fi

# ============================================================================
#  PHASE 6 — Run Database Migrations
# ============================================================================
banner "Phase 6: Database Migrations"

if [[ -f "$APP_DIR/migrator/FC.Engine.Migrator.dll" ]]; then
    info "Running database migrator…"

    # Run migrator as the app user
    sudo -u "$APP_USER" \
        ConnectionStrings__FcEngine="$CONN_STR" \
        Seeding__AutoSeed=true \
        Seeding__SchemaFilePath="$APP_DIR/schema/schema.sql" \
        Seeding__MetadataSchemaPath="$APP_DIR/scripts/create-metadata-schema.sql" \
        Seeding__ReferenceDataPath="$APP_DIR/scripts/seed-reference-data.sql" \
        DefaultAdmin__Password="$ADMIN_PASSWORD" \
        dotnet "$APP_DIR/migrator/FC.Engine.Migrator.dll" \
        2>&1 | tail -20

    success "Migrations completed"
else
    warn "Migrator not found — skipping (ensure DB schema is current)"
fi

# ============================================================================
#  PHASE 7 — Create systemd Services
# ============================================================================
banner "Phase 7: Systemd Services"

create_service() {
    local svc_name="$1"     # e.g. fcengine-api
    local display="$2"      # e.g. FC Engine API
    local dll_name="$3"     # e.g. FC.Engine.Api.dll
    local work_dir="$4"     # e.g. /opt/fcengine/api
    local port="$5"         # e.g. 5100
    local extra_env="$6"    # additional Environment= lines

    local svc_file="/etc/systemd/system/${svc_name}.service"

    info "Creating service: ${svc_name} (port ${port})…"

    cat > "$svc_file" <<UNIT
[Unit]
Description=${display}
After=network.target mssql-server.service
Wants=mssql-server.service

[Service]
Type=notify
User=${APP_USER}
Group=${APP_USER}
WorkingDirectory=${work_dir}
ExecStart=/usr/bin/dotnet ${work_dir}/${dll_name}
Restart=always
RestartSec=10
KillSignal=SIGINT
TimeoutStopSec=30
SyslogIdentifier=${svc_name}
StandardOutput=journal
StandardError=journal

# Environment
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:${port}
Environment=DOTNET_CLI_TELEMETRY_OPTOUT=1
Environment=DOTNET_NOLOGO=1
Environment=ConnectionStrings__FcEngine=${CONN_STR}
${extra_env}

# Hardening
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=${work_dir} ${LOG_DIR} ${APP_DIR}/uploads ${APP_DIR}/keys

# Memory limits (adjust per server capacity)
MemoryMax=1G
MemoryHigh=768M

[Install]
WantedBy=multi-user.target
UNIT

    systemctl daemon-reload
    systemctl enable "$svc_name" > /dev/null 2>&1
    success "Service ${svc_name} created"
}

# ── API Service ────────────────────────────────────────────────────────────
API_EXTRA="Environment=Jwt__SigningKeyPath=${JWT_KEY_PATH}
Environment=Seeding__SchemaFilePath=${APP_DIR}/schema/schema.sql
Environment=ApiKey=${API_KEY:-}"

create_service "fcengine-api" "FC Engine API" \
    "FC.Engine.Api.dll" "$APP_DIR/api" "$API_PORT" "$API_EXTRA"

# ── Admin Service ──────────────────────────────────────────────────────────
ADMIN_EXTRA="Environment=HealthCheck__ApiBaseUrl=http://127.0.0.1:${API_PORT}
Environment=FileStorage__BasePath=${APP_DIR}/uploads
Environment=FileStorage__UrlPath=/uploads"

create_service "fcengine-admin" "FC Engine Admin Portal" \
    "FC.Engine.Admin.dll" "$APP_DIR/admin" "$ADMIN_PORT" "$ADMIN_EXTRA"

# ── Portal Service ─────────────────────────────────────────────────────────
PORTAL_EXTRA="Environment=EngineApi__BaseUrl=http://127.0.0.1:${API_PORT}
Environment=RegOS__PortalBaseUrl=https://${DOMAIN_PORTAL}
Environment=FileStorage__BasePath=${APP_DIR}/uploads
Environment=FileStorage__UrlPath=/uploads
Environment=DOTNET_GCConserveMemory=9
Environment=DOTNET_GCHeapHardLimit=0x1C000000"

create_service "fcengine-portal" "FC Engine Portal" \
    "FC.Engine.Portal.dll" "$APP_DIR/portal" "$PORTAL_PORT" "$PORTAL_EXTRA"

# ============================================================================
#  PHASE 8 — Nginx Reverse Proxy
# ============================================================================
banner "Phase 8: Nginx Reverse Proxy"

if ! command -v nginx &>/dev/null; then
    info "Installing Nginx…"
    apt-get install -y -qq nginx > /dev/null
fi

systemctl enable nginx > /dev/null 2>&1

# Remove default site
rm -f /etc/nginx/sites-enabled/default

# ── Shared Nginx settings ─────────────────────────────────────────────────
cat > /etc/nginx/conf.d/fcengine-shared.conf <<'NGINX_SHARED'
# FC Engine — shared proxy settings
proxy_http_version 1.1;
proxy_set_header   Host              $host;
proxy_set_header   X-Real-IP         $remote_addr;
proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
proxy_set_header   X-Forwarded-Proto $scheme;

# WebSocket support (Blazor Server + SignalR)
map $http_upgrade $connection_upgrade {
    default upgrade;
    ''      close;
}

# Security headers
add_header X-Content-Type-Options    "nosniff"             always;
add_header X-Frame-Options           "SAMEORIGIN"          always;
add_header X-XSS-Protection          "1; mode=block"       always;
add_header Referrer-Policy           "strict-origin-when-cross-origin" always;
add_header Permissions-Policy        "camera=(), microphone=(), geolocation=()" always;

# File upload limit (match Kestrel settings)
client_max_body_size 50M;
NGINX_SHARED

# ── Generate Nginx site config ─────────────────────────────────────────────
generate_nginx_site() {
    local domain="$1"
    local port="$2"
    local site_name="$3"
    local conf_file="/etc/nginx/sites-available/${site_name}"

    cat > "$conf_file" <<NGINX_SITE
# ${site_name} — ${domain}
server {
    listen 80;
    listen [::]:80;
    server_name ${domain};

    # Let's Encrypt challenge
    location /.well-known/acme-challenge/ {
        root /var/www/certbot;
        allow all;
    }

    # Redirect to HTTPS (enabled after SSL provisioning)
    location / {
        return 301 https://\$host\$request_uri;
    }
}

server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name ${domain};

    # SSL — placeholder until certbot provisions real certs
    ssl_certificate     /etc/nginx/ssl/selfsigned.crt;
    ssl_certificate_key /etc/nginx/ssl/selfsigned.key;

    # Modern TLS config
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256:ECDHE-ECDSA-AES256-GCM-SHA384:ECDHE-RSA-AES256-GCM-SHA384;
    ssl_prefer_server_ciphers off;
    ssl_session_cache shared:SSL:10m;
    ssl_session_timeout 1d;
    ssl_session_tickets off;

    # HSTS (enable after confirming SSL works)
    # add_header Strict-Transport-Security "max-age=63072000; includeSubDomains; preload" always;

    # Static files — aggressive caching
    location ~* \.(css|js|woff2?|ttf|eot|svg|png|jpg|jpeg|gif|ico|webp)$ {
        proxy_pass http://127.0.0.1:${port};
        expires 30d;
        add_header Cache-Control "public, immutable";
        access_log off;
    }

    # Blazor framework files
    location /_framework/ {
        proxy_pass http://127.0.0.1:${port};
        expires 7d;
        add_header Cache-Control "public";
    }

    # Blazor SignalR/WebSocket endpoint
    location /_blazor {
        proxy_pass http://127.0.0.1:${port};
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection \$connection_upgrade;
        proxy_read_timeout 86400s;
        proxy_send_timeout 86400s;
    }

    # SignalR hubs (Portal)
    location /hubs/ {
        proxy_pass http://127.0.0.1:${port};
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection \$connection_upgrade;
        proxy_read_timeout 86400s;
        proxy_send_timeout 86400s;
    }

    # Health check endpoint
    location /health {
        proxy_pass http://127.0.0.1:${port};
        access_log off;
    }

    # All other requests
    location / {
        proxy_pass http://127.0.0.1:${port};
        proxy_buffering off;
        proxy_read_timeout 300s;
    }
}
NGINX_SITE

    ln -sf "$conf_file" "/etc/nginx/sites-enabled/${site_name}"
    success "Nginx site configured: ${domain} → :${port}"
}

# ── Generate self-signed placeholder cert ──────────────────────────────────
if [[ ! -f /etc/nginx/ssl/selfsigned.crt ]]; then
    info "Generating self-signed placeholder certificate…"
    mkdir -p /etc/nginx/ssl
    openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
        -keyout /etc/nginx/ssl/selfsigned.key \
        -out /etc/nginx/ssl/selfsigned.crt \
        -subj "/CN=localhost" 2>/dev/null
    success "Placeholder SSL cert created"
fi

# ── Create site configs ────────────────────────────────────────────────────
generate_nginx_site "$DOMAIN_API"    "$API_PORT"    "fcengine-api"
generate_nginx_site "$DOMAIN_ADMIN"  "$ADMIN_PORT"  "fcengine-admin"
generate_nginx_site "$DOMAIN_PORTAL" "$PORTAL_PORT" "fcengine-portal"

# ── Validate and reload ───────────────────────────────────────────────────
nginx -t 2>/dev/null && {
    systemctl reload nginx
    success "Nginx configuration valid and reloaded"
} || fatal "Nginx configuration test failed"

# ============================================================================
#  PHASE 9 — SSL Certificates (Let's Encrypt)
# ============================================================================
banner "Phase 9: SSL Certificates"

if [[ "$ENABLE_SSL" == "true" ]]; then
    if ! command -v certbot &>/dev/null; then
        info "Installing Certbot…"
        apt-get install -y -qq certbot python3-certbot-nginx > /dev/null
    fi

    mkdir -p /var/www/certbot

    info "Requesting SSL certificates for all domains…"
    certbot --nginx --non-interactive --agree-tos \
        --email "$SSL_EMAIL" \
        --redirect \
        -d "$DOMAIN_API" \
        -d "$DOMAIN_ADMIN" \
        -d "$DOMAIN_PORTAL" \
        2>&1 | tail -5

    # Ensure auto-renewal timer is active
    systemctl enable certbot.timer > /dev/null 2>&1
    systemctl start certbot.timer

    success "SSL certificates provisioned — auto-renewal enabled"
else
    warn "SSL disabled (ENABLE_SSL=false) — using self-signed certs"
fi

# ============================================================================
#  PHASE 10 — Firewall (UFW)
# ============================================================================
banner "Phase 10: Firewall"

info "Configuring UFW firewall…"
ufw --force reset > /dev/null 2>&1

ufw default deny incoming > /dev/null
ufw default allow outgoing > /dev/null

ufw allow ssh > /dev/null
ufw allow 80/tcp > /dev/null     # HTTP
ufw allow 443/tcp > /dev/null    # HTTPS

# SQL Server — only from localhost (no external access)
ufw deny 1433/tcp > /dev/null

ufw --force enable > /dev/null
success "Firewall configured (SSH, HTTP, HTTPS allowed; SQL Server blocked externally)"

# ============================================================================
#  PHASE 11 — Fail2Ban
# ============================================================================
banner "Phase 11: Fail2Ban"

cat > /etc/fail2ban/jail.local <<'F2B'
[DEFAULT]
bantime  = 3600
findtime = 600
maxretry = 5

[sshd]
enabled = true

[nginx-http-auth]
enabled = true

[nginx-limit-req]
enabled  = true
filter   = nginx-limit-req
logpath  = /var/log/nginx/error.log
maxretry = 10
F2B

systemctl enable fail2ban > /dev/null 2>&1
systemctl restart fail2ban
success "Fail2Ban configured"

# ============================================================================
#  PHASE 12 — Log Rotation
# ============================================================================
banner "Phase 12: Log Rotation"

cat > /etc/logrotate.d/fcengine <<LOGROTATE
${LOG_DIR}/*/*.log {
    daily
    missingok
    rotate 30
    compress
    delaycompress
    notifempty
    create 0640 ${APP_USER} ${APP_USER}
    sharedscripts
    postrotate
        systemctl reload fcengine-api fcengine-admin fcengine-portal > /dev/null 2>&1 || true
    endscript
}
LOGROTATE

success "Log rotation configured (30 days retention)"

# ============================================================================
#  PHASE 13 — Database Backup Cron
# ============================================================================
banner "Phase 13: Database Backups"

BACKUP_SCRIPT="$APP_DIR/scripts/backup-db.sh"

cat > "$BACKUP_SCRIPT" <<'BACKUP'
#!/usr/bin/env bash
# FC Engine — Automated SQL Server Backup
set -euo pipefail

BACKUP_DIR="${BACKUP_DIR:-/opt/fcengine/backups}"
DB_NAME="${DB_NAME:-FcEngine}"
DB_PASSWORD="${DB_PASSWORD}"
SQL_PORT="${SQL_PORT:-1433}"
RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-14}"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="${BACKUP_DIR}/${DB_NAME}_${TIMESTAMP}.bak"

# Run backup
/opt/mssql-tools18/bin/sqlcmd -S "127.0.0.1,${SQL_PORT}" -U sa -P "$DB_PASSWORD" -C \
    -Q "BACKUP DATABASE [${DB_NAME}] TO DISK = N'${BACKUP_FILE}' WITH COMPRESSION, INIT, STATS = 10" \
    2>/dev/null

if [[ -f "$BACKUP_FILE" ]]; then
    SIZE=$(du -sh "$BACKUP_FILE" | cut -f1)
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] Backup OK: ${BACKUP_FILE} (${SIZE})"
else
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] Backup FAILED" >&2
    exit 1
fi

# Prune old backups
find "$BACKUP_DIR" -name "${DB_NAME}_*.bak" -mtime +"$RETENTION_DAYS" -delete 2>/dev/null
PRUNED=$(find "$BACKUP_DIR" -name "${DB_NAME}_*.bak" | wc -l)
echo "[$(date '+%Y-%m-%d %H:%M:%S')] Retained ${PRUNED} backup(s)"
BACKUP

chmod +x "$BACKUP_SCRIPT"

# ── Environment wrapper for cron ───────────────────────────────────────────
BACKUP_CRON_WRAPPER="$APP_DIR/scripts/backup-cron.sh"
cat > "$BACKUP_CRON_WRAPPER" <<CRON_WRAP
#!/usr/bin/env bash
export BACKUP_DIR="$BACKUP_DIR"
export DB_NAME="$DB_NAME"
export DB_PASSWORD="$DB_PASSWORD"
export SQL_PORT="$SQL_PORT"
export BACKUP_RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-14}"
exec "$BACKUP_SCRIPT" >> "$LOG_DIR/backup.log" 2>&1
CRON_WRAP
chmod +x "$BACKUP_CRON_WRAPPER"

# ── Install cron job (daily at 02:00) ─────────────────────────────────────
CRON_ENTRY="0 2 * * * ${BACKUP_CRON_WRAPPER}"
(crontab -l 2>/dev/null | grep -v "backup-cron.sh"; echo "$CRON_ENTRY") | crontab -

success "Database backup configured (daily at 02:00, ${BACKUP_RETENTION_DAYS:-14}-day retention)"

# ============================================================================
#  PHASE 14 — Start Services
# ============================================================================
banner "Phase 14: Starting Services"

for svc in fcengine-api fcengine-admin fcengine-portal; do
    info "Starting ${svc}…"
    systemctl restart "$svc"
    sleep 3

    if systemctl is-active --quiet "$svc"; then
        success "${svc} is running"
    else
        err "${svc} failed to start — check: journalctl -u ${svc} -n 50"
    fi
done

# ============================================================================
#  PHASE 15 — Health Checks & Verification
# ============================================================================
banner "Phase 15: Health Checks"

check_health() {
    local name="$1"
    local url="$2"
    local max_wait="${3:-15}"

    for i in $(seq 1 "$max_wait"); do
        if curl -sf "$url" > /dev/null 2>&1; then
            success "${name}: healthy (${url})"
            return 0
        fi
        sleep 1
    done

    warn "${name}: not responding at ${url} — may still be starting"
    return 1
}

sleep 5  # Give services a moment to fully start

check_health "API"    "http://127.0.0.1:${API_PORT}/health"
check_health "Admin"  "http://127.0.0.1:${ADMIN_PORT}/"
check_health "Portal" "http://127.0.0.1:${PORTAL_PORT}/"

# ── SQL Server check ──────────────────────────────────────────────────────
if /opt/mssql-tools18/bin/sqlcmd -S "127.0.0.1,${SQL_PORT}" -U sa -P "$DB_PASSWORD" -C \
    -Q "SELECT COUNT(*) AS TableCount FROM [${DB_NAME}].INFORMATION_SCHEMA.TABLES" 2>/dev/null | grep -q "[0-9]"; then
    success "SQL Server: database '${DB_NAME}' accessible"
else
    warn "SQL Server: could not verify database tables"
fi

# ============================================================================
#  PHASE 16 — Summary
# ============================================================================
banner "Deployment Complete"

cat <<SUMMARY

  ${GREEN}FC Engine has been deployed successfully.${NC}

  ┌─────────────────────────────────────────────────────────────┐
  │  Service          │  URL                                    │
  ├─────────────────────────────────────────────────────────────┤
  │  Admin Portal     │  https://${DOMAIN_ADMIN}                │
  │  Portal           │  https://${DOMAIN_PORTAL}               │
  │  API              │  https://${DOMAIN_API}                  │
  │  API Health       │  https://${DOMAIN_API}/health           │
  │  Swagger Docs     │  https://${DOMAIN_API}/swagger/ui       │
  │  SQL Server       │  127.0.0.1:${SQL_PORT} (local only)    │
  └─────────────────────────────────────────────────────────────┘

  ${BOLD}Systemd Services:${NC}
    systemctl status fcengine-api
    systemctl status fcengine-admin
    systemctl status fcengine-portal

  ${BOLD}Logs:${NC}
    journalctl -u fcengine-api -f
    journalctl -u fcengine-admin -f
    journalctl -u fcengine-portal -f

  ${BOLD}Database Backups:${NC}
    ${BACKUP_DIR}/ (daily at 02:00, ${BACKUP_RETENTION_DAYS:-14}-day retention)

  ${BOLD}SSL Renewal:${NC}
    certbot renew --dry-run

  ${BOLD}Management Script:${NC}
    bash ${SCRIPT_DIR}/manage.sh {status|restart|logs|backup|update}

SUMMARY
