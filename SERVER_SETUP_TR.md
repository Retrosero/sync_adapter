# FieldOps Bridge — Sunucu Kurulum Rehberi

> Bu rehber, FieldOps Bridge uygulamasını **üretim (production) sunucusuna** kurmak için gereken tüm adımları içerir.
> Test ortamı için README.md'ye bakın.

---

## Gereksinimler

### Minimum Donanım
- **CPU:** 2 çekirdek (önerilen: 4+)
- **RAM:** 4 GB (önerilen: 8 GB)
- **Disk:** 40 GB SSD
- **OS:** Ubuntu 22.04 LTS (veya 24.04)

### Kurulacak Yazılımlar
| Yazılım | Versiyon | Açıklama |
|---------|----------|----------|
| Docker | 24+ | API container + PostgreSQL için |
| Docker Compose | v2+ | Çoklu container yönetimi |
| PostgreSQL | 16 | Tavsiye edilen; 14/15 de çalışır |
| Nginx | 1.18+ | Reverse proxy + SSL termination |
| Certbot | — | Let's Encrypt SSL sertifikası |
| .NET SDK | 8 | Agent build etmek için (opsiyonel, agent Windows'ta çalışır) |

---

## Adım 1 — Sunucu Hazırlığı

### 1.1 Sunucuya Bağlan

```bash
ssh root@SUNUCU_IP
```

### 1.2 Sistem Güncellemesi

```bash
apt update && apt upgrade -y
```

### 1.3 Docker Kurulumu

```bash
# Docker kurulumu (resmi script)
curl -fsSL https://get.docker.com | sh

# Docker compose plugin
apt install docker-compose-plugin -y

# Docker'u root olmayan kullanıcıyla kullanabilmek için
usermod -aG docker $USER
newgrp docker
```

> **Önemli:** Docker kurulumundan sonra **oturumu kapatıp tekrar açın** (SSH).

### 1.4 Docker Servisinin Çalıştığını Doğrula

```bash
docker --version
docker compose version
docker run hello-world
```

---

## Adım 2 — PostgreSQL Kurulumu

### 2.1 PostgreSQL Kurulumu

```bash
apt install -y postgresql postgresql-contrib
```

### 2.2 PostgreSQL Servisini Başlat

```bash
systemctl enable postgresql
systemctl start postgresql
systemctl status postgresql
```

### 2.3 Veritabanı ve Kullanıcı Oluştur

```bash
sudo -u postgres psql <<'EOF'
-- Veritabanı oluştur
CREATE DATABASE fieldops;

-- Kullanıcı oluştur
CREATE USER fieldops_user WITH PASSWORD 'BURAYA_GUVENLI_SIFRE_YAZ';

-- Yetkileri ver
GRANT ALL PRIVILEGES ON DATABASE fieldops TO fieldops_user;

-- Şemaya tam yetki
\c fieldops
GRANT ALL ON SCHEMA public TO fieldops_user;
GRANT CREATE ON SCHEMA public TO fieldops_user;

-- Giriş yapıp yapamayacağını test et
EOF
```

> ⚠️ **Şifreyi güvenli bir yere not alın.** Bu şifreyi `appsettings.Production.json` ve `docker-compose.yml` dosyalarında kullanacaksınız.

### 2.4 Uzaktan Bağlantı İzni (opsiyonel)

Sunucudan farklı bir makineden bağlanacaksanız:

```bash
# PostgreSQL dinleme adresini ayarla
sed -i "s/#listen_addresses = 'localhost'/listen_addresses = '*'/" /etc/postgresql/16/main/postgresql.conf

# pg_hba.conf'a IP izni ekle (kendi IP'nizi yazın)
echo "host    fieldops         fieldops_user     0.0.0.0/0             md5" >> /etc/postgresql/16/main/pg_hba.conf

systemctl restart postgresql
```

### 2.5 Bağlantı Testi

```bash
psql -h localhost -U fieldops_user -d fieldops -W
# Şifre: yukarıda belirlediğiniz şifre
# Başarılı bağlantı: fieldops=# prompt
\q
```

---

## Adım 3 — Docker Compose ile API Kurulumu

### 3.1 Projeyi Sunucuya Al

```bash
cd /opt
git clone https://github.com/Retrosero/sync_adapter.git
cd sync_adapter/server
```

### 3.2 Production Environment Dosyası Oluştur

```bash
cat > .env.production <<'EOF'
# PostgreSQL bağlantısı
FIELDOPS_DB_HOST=localhost
FIELDOPS_DB_PORT=5432
FIELDOPS_DB_NAME=fieldops
FIELDOPS_DB_USER=fieldops_user
FIELDOPS_DB_PASSWORD=BURAYA_GUVENLI_SIFRE_YAZ

# API ayarları
API_BASE_URL=https://api.alanadiniz.com
API_PORT=8080

# CORS (Admin SPA'nın çalıştığı adres)
CORS_ORIGINS=https://admin.alanadiniz.com,https://admin.alanadiniz.com:5173
EOF
```

### 3.3 Production appsettings Dosyası

```bash
cat > src/FieldOps.Api/appsettings.Production.json <<'EOF'
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "/app/Logs/fieldops-api-.log",
          "rollingInterval": "Day",
          "rollOnFileSizeLimit": true,
          "fileSizeLimitBytes": 10485760,
          "retainedFileCountLimit": 30
        }
      }
    ]
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "Postgres": "Host=${FIELDOPS_DB_HOST};Port=${FIELDOPS_DB_PORT};Database=${FIELDOPS_DB_NAME};Username=${FIELDOPS_DB_USER};Password=${FIELDOPS_DB_PASSWORD};SSL Mode=Prefer"
  },
  "Cors": {
    "AllowedOrigins": ["${CORS_ORIGINS}"]
  }
}
EOF
```

### 3.4 Docker Compose Dosyasını Güncelle

Mevcut `docker-compose.yml`'yi production için güncelleyin:

```bash
# Mevcut dosyayı yedekle
cp docker-compose.yml docker-compose.yml.orig

# Yeni compose dosyası oluştur
cat > docker-compose.yml <<'EOF'
services:
  api:
    build:
      context: .
      dockerfile: src/FieldOps.Api/Dockerfile
    container_name: fieldops-api
    restart: unless-stopped
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - ConnectionStrings__Postgres=Host=${FIELDOPS_DB_HOST};Port=${FIELDOPS_DB_PORT};Database=${FIELDOPS_DB_NAME};Username=${FIELDOPS_DB_USER};Password=${FIELDOPS_DB_PASSWORD};SSL Mode=Prefer
      - Serilog__WriteTo__0__Args__path=/app/Logs/fieldops-api-.log
    ports:
      - "127.0.0.1:8080:8080"   # Sadece localhost'tan erişilebilir (nginx reverse proxy arkasında)
    volumes:
      - fieldops_api_logs:/app/Logs
    depends_on:
      postgres:
        condition: service_healthy

  postgres:
    image: postgres:16-alpine
    container_name: fieldops-postgres
    restart: unless-stopped
    environment:
      - POSTGRES_DB=fieldops
      - POSTGRES_USER=fieldops_user
      - POSTGRES_PASSWORD=${FIELDOPS_DB_PASSWORD}
    volumes:
      - fieldops_pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U fieldops_user -d fieldops"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  fieldops_api_logs:
  fieldops_pgdata:
EOF
```

### 3.5 Docker Image Build Et ve Çalıştır

```bash
docker compose build --no-cache
docker compose up -d
docker compose ps
```

### 3.6 API'nin Çalıştığını Doğrula

```bash
curl http://localhost:8080/health
# Beklenen yanıt: {"status":"ok"}
```

### 3.7 Logları İzle

```bash
docker compose logs -f api
```

---

## Adım 4 — Nginx + SSL Kurulumu

### 4.1 Nginx Kurulumu

```bash
apt install -y nginx
```

### 4.2 SSL Sertifikası (Let's Encrypt)

```bash
# Alan adınızı buraya yazın
DOMAIN="api.alanadiniz.com"

# Certbot kurulumu
apt install -y certbot python3-certbot-nginx

# Sertifika al
certbot --nginx -d $DOMAIN --non-interactive --agree-tos -m admin@alanadiniz.com
```

### 4.3 Nginx Yapılandırması

```bash
cat > /etc/nginx/sites-available/fieldops <<EOF
server {
    listen 80;
    server_name $DOMAIN;
    return 301 https://\$host\$request_uri;
}

server {
    listen 443 ssl http2;
    server_name $DOMAIN;

    ssl_certificate     /etc/letsencrypt/live/$DOMAIN/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/$DOMAIN/privkey.pem;
    ssl_protocols       TLSv1.2 TLSv1.3;
    ssl_ciphers         ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256;

    client_max_body_size 10M;

    location / {
        proxy_pass         http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade \$http_upgrade;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host \$host;
        proxy_set_header   X-Real-IP \$remote_addr;
        proxy_set_header   X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto \$scheme;
        proxy_cache_bypass \$http_upgrade;
        proxy_read_timeout 60s;
    }

    location /swagger {
        proxy_pass         http://127.0.0.1:8080;
        proxy_set_header   Host \$host;
    }
}
EOF

# Symlink oluştur ve eski default'u kaldır
rm -f /etc/nginx/sites-enabled/default
ln -sf /etc/nginx/sites-available/fieldops /etc/nginx/sites-enabled/fieldops

# Test et ve yeniden başlat
nginx -t
systemctl reload nginx
systemctl enable nginx
```

### 4.4 SSL Otomatik Yenileme

```bash
certbot renew --dry-run
systemctl status certbot.timer
```

---

## Adım 5 — Veritabanı Şemasını Uygula

### 5.1 Migration Çalıştır (API ayağa kalktığında otomatik yapar, ama manuel de yapılabilir)

```bash
# API container içinde EF Core migrate çalıştır
docker compose exec api dotnet ef database update \
  --connection "Host=${FIELDOPS_DB_HOST};Port=${FIELDOPS_DB_PORT};Database=${FIELDOPS_DB_NAME};Username=${FIELDOPS_DB_USER};Password=${FIELDOPS_DB_PASSWORD}" \
  --project /app/../FieldOps.Infrastructure/FieldOps.Infrastructure.csproj \
  --startup-project /app/FieldOps.Api.csproj
```

> **Alternatif:** Migration yerine SQL dosyasını doğrudan çalıştırın:
> ```bash
> psql -h localhost -U fieldops_user -d fieldops -W < docs/001-initial-schema.sql
> ```

### 5.2 Admin Tenant + API Key Seed Et

```bash
psql -h localhost -U fieldops_user -d fieldops -W <<'EOF'
-- SYSTEM admin tenant (admin key üretimi için)
INSERT INTO fieldops.tenants (id, name, code, is_active, is_system, created_at, updated_at)
VALUES (
  '00000000-0000-0000-0000-000000000001',
  'SYSTEM',
  'SYSTEM',
  true,
  true,
  NOW(),
  NOW()
) ON CONFLICT (id) DO NOTHING;

-- Admin API key (plain: fo_live_rU4yGOT-lSWArqa87MMOB_u8UxsApjEFtVGYiAIjfyk)
INSERT INTO fieldops.tenant_api_keys (id, tenant_id, key_prefix, key_hash, scope, is_active, created_at)
VALUES (
  '00000000-0000-0000-0000-000000000002',
  '00000000-0000-0000-0000-000000000001',
  'fo_live_rU4y',
  '7efc885e6fd09d0083d878858f651fb76e881336b763fe1ce96ad74c1ddde42e',
  'Admin',
  true,
  NOW()
) ON CONFLICT (id) DO NOTHING;
EOF
```

### 5.3 Admin Key ile Test Et

```bash
curl -H "X-Api-Key: fo_live_rU4yGOT-lSWArqa87MMOB_u8UxsApjEFtVGYiAIjfyk" \
     https://api.alanadiniz.com/api/v1/admin/tenants
```

---

## Adım 6 — Admin SPA Kurulumu (Nginx)

### 6.1 SPA Build Dosyalarını Sunucuya Kopyala

Geliştirme makinenizde:

```bash
cd admin-ui
npm install
npm run build
# dist/ klasörünü sunucuya at
scp -r dist/ root@SUNUCU_IP:/var/www/fieldops-admin/
```

Sunucuda:

```bash
mkdir -p /var/www/fieldops-admin
# dist/ içeriğini /var/www/fieldops-admin/ kopyala
```

### 6.2 Admin SPA Nginx Yapılandırması

```bash
cat > /etc/nginx/sites-available/fieldops-admin <<EOF
server {
    listen 80;
    server_name admin.alanadiniz.com;
    return 301 https://\$host\$request_uri;
}

server {
    listen 443 ssl http2;
    server_name admin.alanadiniz.com;

    ssl_certificate     /etc/letsencrypt/live/admin.alanadiniz.com/fullchain.pem;
    ssl_certificate_key  /etc/letsencrypt/live/admin.alanadiniz.com/privkey.pem;
    ssl_protocols        TLSv1.2 TLSv1.3;

    root /var/www/fieldops-admin;
    index index.html;

    location / {
        try_files \$uri \$uri/ /index.html;
    }

    # API proxy (CORS sorunlarını önlemek için)
    location /api/ {
        proxy_pass         http://127.0.0.1:8080/api/;
        proxy_http_version 1.1;
        proxy_set_header   Host \$host;
        proxy_set_header   X-Real-IP \$remote_addr;
        proxy_set_header   X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto \$scheme;
    }
}
EOF

ln -sf /etc/nginx/sites-available/fieldops-admin /etc/nginx/sites-enabled/
nginx -t
systemctl reload nginx
```

### 6.3 Admin SPA SSL Sertifikası

```bash
certbot --nginx -d admin.alanadiniz.com --non-interactive --agree-tos -m admin@alanadiniz.com
systemctl reload nginx
```

---

## Adım 7 — Windows Agent Kurulumu

Agent, **sunucuda değil, müşterinin Windows makinelerinde** çalışır.

### 7.1 Build (geliştirme makinenizde)

```bash
cd agent
dotnet publish src/SyncAdapter.Agent/SyncAdapter.Agent.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -o ../publish/agent
```

### 7.2 Müşteri Makinelerine Dağıtım

Müşteri Windows makinesinde:

1. `publish/agent/` klasörünü kopyalayın (örn. `C:\FieldOpsAgent`)
2. `appsettings.json` dosyasını düzenleyin:

```json
{
  "Agent": {
    "AgentId": "AGEN-001",
    "TenantId": "MUSTERI-TENANT-GUID",
    "ApiKey": "fo_live_xxxx",
    "ApiBaseUrl": "https://api.alanadiniz.com"
  },
  "Mikro": {
    "Server": "MIKRO_SUNUCU",
    "Database": "MikroDB_V15_02",
    "UseWindowsAuth": true
  },
  "Sync": {
    "IntervalSeconds": 60,
    "Tables": [ ... ]
  }
}
```

3. Windows Service olarak kurun:

```powershell
sc.exe create "FieldOps Agent" binPath="C:\FieldOpsAgent\SyncAdapter.Agent.exe" start=auto
sc.exe start "FieldOps Agent"
```

4. Tray simgesinden izleyin (WinForms UI geldiğinde)

---

## Adım 8 — Güvenlik Kontrolleri

### 8.1 Firewall Kuralları

```bash
# Sadece SSH, HTTP, HTTPS'e izin ver
ufw default deny incoming
ufw allow 22/tcp    # SSH
ufw allow 80/tcp    # HTTP
ufw allow 443/tcp   # HTTPS
ufw enable
ufw status
```

### 8.2 PostgreSQL'e Sadece Local Erişim

```bash
# /etc/postgresql/16/main/postgresql.conf
listen_addresses = 'localhost'   # Sadece Unix socket + localhost
```

### 8.3 Docker Socket Koruması

Docker daemon'u root yetkisi gerektirir. Ek koruma için:

```bash
# Audit kuralları (opsiyonel)
apt install auditd -y
echo "-w /var/run/docker.sock -p rwxa -k docker" >> /etc/audit/rules.d/docker.rules
service auditd restart
```

### 8.4 Environment Değişkenlerini Koruma

```bash
# .env.production dosyasını sadece root okuyabilir
chmod 600 .env.production
```

---

## Adım 9 — İzleme ve Log Yönetimi

### 9.1 Docker Log Boyutu Sınırı

```bash
# /etc/docker/daemon.json
{
  "log-driver": "json-file",
  "log-opts": {
    "max-size": "10m",
    "max-file": "3"
  }
}
systemctl restart docker
```

### 9.2 Logrotate

```bash
cat > /etc/logrotate.d/fieldops <<'EOF'
/var/lib/docker/volumes/server_fieldops_api_logs/_data/*.log {
    daily
    rotate 30
    compress
    delaycompress
    notifempty
    create 064400 root root
    postrotate
        docker kill -s SIGUSR1 fieldops-api > /dev/null 2>&1 || true
    endscript
}
EOF
```

### 9.3 Basit Health Check Cron

```bash
# Her 5 dakikada API health kontrolü
*/5 * * * * curl -sf https://api.alanadiniz.com/health || \
  echo "FieldOps API DOWN at $(date)" | mail -s "FieldOps Alert" admin@alanadiniz.com
```

---

## Hızlı Kontrol Listesi

- [ ] Docker + Docker Compose kurulu ve çalışıyor
- [ ] PostgreSQL kurulu, veritabanı ve kullanıcı oluşturuldu
- [ ] API Docker container build edildi ve çalışıyor
- [ ] `GET /health` → `200 {"status":"ok"}`
- [ ] Nginx + SSL kurulu, API'ye proxy yapıyor
- [ ] Admin API key ile tenant oluşturulabiliyor
- [ ] Admin SPA build edildi, Nginx ile sunuluyor
- [ ] Windows Firewall sadece HTTP/HTTPS'e açık
- [ ] `.env.production` chmod 600
- [ ] Logrotate yapılandırıldı
- [ ] Certbot SSL renew timer aktif

---

## Sorun Giderme

### API 502 veriyor
```bash
docker compose logs api
docker compose ps
# Container çalışıyor mu?
# postgres container healthy mi?
curl http://127.0.0.1:8080/health
```

### PostgreSQL bağlantı hatası
```bash
sudo -u postgres psql -d fieldops -c "SELECT 1"
# Bağlantı başarısız: pg_hba.conf kontrol et
```

### SSL sertifikası yenilenmiyor
```bash
certbot renew --force-renewal
systemctl status certbot.timer
```

---

## Destek ve İletişim

Sorular için GitHub Issues kullanın: [github.com/Retrosero/sync_adapter/issues](https://github.com/Retrosero/sync_adapter/issues)
