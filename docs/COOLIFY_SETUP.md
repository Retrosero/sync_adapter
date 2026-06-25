# Coolify Kurulum Rehberi — FieldOps Bridge

Coolify, GitHub'a bağlanıp Docker tabanlı uygulamaları otomatik deploy eden bir PaaS aracıdır.
Elinde bir sunucu (VPS) varsa, Docker + Nginx + Certbot'u manuel kurmaya gerek kalmaz.

---

## Coolify Kurulumu (Sunucuda)

Sunucuda henüz Coolify yoksa:

```bash
curl -fsSL https://coolify.io/install.sh | bash
```

Kurulumdan sonra `https://SUNUCU_IP:8000` üzerinden Coolify paneline erişilir.
İlk açılışta admin hesabı oluştur.

> **Ön koşul:** Sunucuda Docker kurulu olmalı.
> ```bash
> curl -fsSL https://get.docker.com | sh
> ```

---

## Adım 1 — Coolify'a GitHub Hesabını Bağla

1. Coolify panelinde **`Settings → Sources → Add Source → GitHub`** seç
2. **GitHub Personal Access Token** ver (PAT) — [github.com/settings/tokens](https://github.com/settings/tokens) adresinden oluştur. Gerekli izinler: `repo` (tüm repo erişimi).
3. Token'a gerekli izinleri ver (repo, workflow)
4. **Install & Authorize** — `Retrosero/sync_adapter` reposu görünür olmalı

---

## Adım 2 — PostgreSQL Veritabanı Oluştur

Coolify'ın kendi veritabanı servisini kullanacağız:

1. Panelde **`New Resource → Database → PostgreSQL`** seç
2. Ayarlar:
   | Ayar | Değer |
   |------|-------|
   | Name | `fieldops-db` |
   | Image | `postgres:16-alpine` |
   | Database | `fieldops` |
   | Username | `fieldops_user` |
   | Password | *(güvenli bir şifre üret, kaydet)* |
   | Publish Port | Boş bırak (internal ağdan erişilecek) |
3. **Deploy** — veritabanı ayağa kalkar
4. Bağlantı bilgilerini kaydet:
   ```
   Host: 10.0.0.x (internal IP — Coolify panelinde görünür)
   Port: 5432
   Database: fieldops
   User: fieldops_user
   Password: <şifren>
   ```

> ⚠️ **Bu şifreyi sakla** — bir daha göremezsin.

---

## Adım 3 — API'yi Deploy Et (FieldOps.Api)

### 3.1 Yeni Application Oluştur

1. **`New Resource → Application → GitHub`** seç
2. Repository: **`Retrosero / sync_adapter`**
3. Branch: **`main`**
4. Build Pack: **`Dockerfile`** ( Coolify otomatik algılar)
5. **Configuration:**

| Ayar | Değer |
|------|-------|
| Name | `fieldops-api` |
| Port | `8080` |
| Base Directory | `/server` |

> `Base Directory: /server` — çünkü Dockerfile `server/src/FieldOps.Api/Dockerfile`'da

### 3.2 Environment Variables

**`New Environment Variable`** butonuyla şunları ekle:

| Variable | Değer | Açıklama |
|----------|-------|----------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Log seviyesi, hata detayları |
| `ConnectionStrings__Postgres` | *(aşağıda)* | PostgreSQL bağlantı stringi |
| `Serilog__MinimumLevel__Default` | `Information` | Log seviyesi |

**`ConnectionStrings__Postgres` değeri** (PostgreSQL servisi deploy olduktan sonra Coolify otomatik IP verir):

```
Host=<COOLIFY_POSTGRES_IP>;Port=5432;Database=fieldops;Username=fieldops_user;Password=<SIFREN>;SSL Mode=Prefer
```

> **Nasıl bulunur?** PostgreSQL servisinin üzerine tıklayınca "Internal Hostname" görünür. Örn: `coolify-postgres.internal` veya IP.

### 3.3 Database Bağı (opsiyonel ama önerilir)

`Settings → Resources` bölümünde PostgreSQL veritabanını bu API'ye bağla.
Bu, Coolify'ın connection string'i otomatik yönetmesini sağlar ve `{{ database.fqdn }}` gibi placeholder'lar kullanılabilir.

### 3.4 Deploy

**Deploy** butonuna tıkla. Coolify:
1. GitHub'dan kodu çeker
2. Docker image build eder
3. Container'ı başlatır
4. Health check yapar (`/health` endpoint'i kontrol edilir)

### 3.5 API Test Et

Deploy tamamlandığında Coolify bir URL verir:
```
https://fieldops-api.COOLIFY_DOMAIN.com
```

Test:
```bash
curl https://fieldops-api.COOLIFY_DOMAIN.com/health
# Beklenen: {"status":"ok"}
```

---

## Adım 4 — Schema Migrasyonu Çalıştır

API başlatıldığında EF Core otomatik migration yapmaz.
Manuel çalıştır:

### Seçenek A: Coolify Exec (Terminal)

1. API servisinin üzerinde **"Terminal"** butonuna tıkla
2. Container shell'inde:

```bash
dotnet ef database update \
  --connection "Host=<HOST>;Port=5432;Database=fieldops;Username=fieldops_user;Password=<SIFRE>" \
  --project /app/../FieldOps.Infrastructure/FieldOps.Infrastructure.csproj \
  --startup-project /app/FieldOps.Api.csproj
```

> Container içinde `dotnet ef` çalışmazsa, gerekli NuGet package'i eksik.
> O zaman **Seçenek B** kullan.

### Seçenek B: psql ile Manuel SQL Çalıştır

1. Local makinende `docs/001-initial-schema.sql` dosyasını indir
2. Coolify'da PostgreSQL servisinde **"Connect"** veya **"Database"** sekmesine git
3. SQL dosyasını yapıştır ve çalıştır

```bash
# Veya psql ile doğrudan
psql -h <COOLIFY_POSTGRES_IP> -U fieldops_user -d fieldops -f docs/001-initial-schema.sql -W
```

### Seçenek C: Seed Admin Key

Admin API key oluşturmak için aynı psql bağlantısıyla:

```sql
-- SYSTEM tenant
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
```

### 4.4 Admin Key Test Et

```bash
curl -H "X-Api-Key: fo_live_rU4yGOT-lSWArqa87MMOB_u8UxsApjEFtVGYiAIjfyk" \
     https://fieldops-api.COOLIFY_DOMAIN.com/api/v1/admin/tenants
```

`[]` veya tenant listesi dönmeli.

---

## Adım 5 — Admin UI'yi Deploy Et

Admin UI (`admin-ui/`) React SPA — statik dosyalar.

### 5.1 Ortam Değişkeni Ekle

`admin-ui/src/utils/api.ts` Coolify için güncellendi:
- `VITE_API_URL` tanımlanmazsa → `/api/v1` (aynı origin, Nginx proxy)
- `VITE_API_URL=https://api.alanadin.com` tanımlanırsa → API: `https://api.alanadin.com/api/v1`

**İki seçenek:**

**Seçenek A — Aynı domain (önerilen):** Admin UI ve API aynı Nginx arkasında, `/api/` proxy var. `VITE_API_URL` tanımlamaya gerek yok.

**Seçenek B — Farklı subdomain:** Admin UI `admin.alanadin.com`, API `api.alanadin.com`:
1. `admin-ui/.env.production` dosyası oluştur:
   ```
   VITE_API_URL=https://api.alanadin.com
   ```
2. Build et: `npm run build`
3. Coolify'a yükle

### 5.2 Build Et (Local)

```bash
cd admin-ui
npm install
npm run build      # dist/ klasörü oluşur
```

> ⚠️ **MAX_PATH hatası alırsan** (Windows uzun yol limiti):
> ```bash
> npm run build
> # Hata olursa:
> cp -r admin-ui C:\fab
> cd C:\fab && npm install && npm run build
> ```

### 5.3 Coolify'a Yükle

**`New Resource → Application → Static`** seç:

| Ayar | Değer |
|------|-------|
| Name | `fieldops-admin` |
| Type | `Static` |
| Build Pack | `Nginx` veya `None` |

Ya da **manual deploy** olarak:
Coolify'ın "Upload" özelliği varsa `dist.zip` dosyasını yükle.
Yoksa Nginx ile ayrı bir servis olarak deploy et.

### 5.4 Nginx Config (Manuel)

Admin UI'yi ayrı bir Docker container'da sunmak istersen:

```bash
# nginx/Dockerfile
FROM nginx:alpine
COPY dist/ /usr/share/nginx/html/
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
```

```nginx
# nginx.conf
server {
    listen 80;
    root /usr/share/nginx/html;
    index index.html;
    location / {
        try_files $uri $uri/ /index.html;
    }
    # API proxy
    location /api/ {
        proxy_pass http://fieldops-api:8080/api/;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

---

## Adım 6 — Domain + SSL (Coolify Built-in)

Coolify'da domain bağlamak çok kolay:

1. API servisinde **`Settings → Domains → Add Domain`** seç
2. Alan adı gir: `api.alanadin.com`
3. DNS kaydını Coolify'ın verdiği IP'ye yönlendir:
   ```
   A  api.alanadin.com  →  SUNUCU_IP
   ```
4. Coolify **otomatik Let's Encrypt SSL** sertifikası üretir ve yeniler

Aynı şekilde `admin.alanadin.com` için de domain ekle.

---

## Adım 7 — Windows Agent Konfigürasyonu

Sunucu deploy olduktan sonra `agent/src/SyncAdapter.Agent/appsettings.json` güncellenir:

```json
{
  "ServerBaseUrl": "https://api.alanadin.com",
  "TenantId": "<Coolify'da oluşturulan tenant ID>",
  "ApiKey": "<Admin SPA'dan üretilen tenant API key>",
  "Mikro": {
    "Server": "GURBUZ",
    "Port": 1433,
    "Database": "MikroDB_V15_02",
    "UseWindowsAuth": true
  }
}
```

---

## Özet — Coolify Panel Sırası

```
1. Sources → GitHub bağla
   ↓
2. New Resource → Database → PostgreSQL
   → fieldops-db (fieldops / fieldops_user / <şifre>)
   ↓
3. New Resource → Application → sync_adapter (GitHub)
   → Base Directory: /server
   → Port: 8080
   → ENV: ASPNETCORE_ENVIRONMENT=Production
   → ENV: ConnectionStrings__Postgres=...
   ↓
4. Manuel migration (psql veya terminal)
   ↓
5. Admin key seed et
   ↓
6. API'yi test et (/health)
   ↓
7. Domain + SSL ekle (api.alanadin.com)
   ↓
8. Admin UI build et → Coolify'a yükle
   ↓
9. Domain + SSL ekle (admin.alanadin.com)
```

---

## Sorun Giderme

### API 502 hatası
```
Coolify → fieldops-api → Logs
Container çalışıyor mu? Health check geçiyor mu?
PostgreSQL bağlantısı doğru mu?
```

### Migration hatası
```
EF Core migration tabloları yok:
→ psql ile docs/001-initial-schema.sql çalıştır
→ Tablolar mevcut ama boş:
→ Seed admin key SQL'ini çalıştır
```

### SSL hatası
```
DNS kaydı henüz yayılmadı (TTL bekle).
Coolify "Force SSL" açık mı?
```

### CORS hatası (Admin UI → API)
```
API'de CORS ayarı yapılmış olmalı.
appsettings.json → Cors → AllowedOrigins
Admin UI'nin domaini buraya eklenmeli.
```

---

## Hızlı Kontrol Listesi

- [ ] Coolify kurulu ve çalışıyor (port 8000)
- [ ] GitHub source bağlı
- [ ] PostgreSQL `fieldops-db` deploy ve healthy
- [ ] API `fieldops-api` deploy ve healthy
- [ ] `GET /health` → `200 {"status":"ok"}`
- [ ] Admin key seed edildi
- [ ] API key test edildi
- [ ] Domain + SSL (api subdomain)
- [ ] Admin UI build + deploy
- [ ] Domain + SSL (admin subdomain)
- [ ] Windows agent `appsettings.json` güncellendi
