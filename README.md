# FieldOps Bridge — Multi-Tenant Veri Köprüsü

Türkiye'deki küçük/orta ölçekli şirketlerin Mikro ERP (v15) verilerini Android saha satış
uygulamasına, **multi-tenant** ve **çift yönlü** olarak taşıyan sistem.

## Çözüm Yapısı

```
workspace/
├── server/                    # FieldOps API (.NET 8 + PostgreSQL)
│   ├── src/
│   │   ├── FieldOps.Api/         # ASP.NET Core Minimal API host
│   │   ├── FieldOps.Application/ # Servis soyutlamaları
│   │   ├── FieldOps.Domain/      # Entity'ler, enum'lar
│   │   ├── FieldOps.Infrastructure/  # EF Core, DbContext, RLS interceptor
│   │   └── FieldOps.Contracts/   # DTO'lar (Android, ajan, admin UI ortak sözleşme)
│   ├── docker-compose.yml        # PostgreSQL + API
│   └── FieldOps.sln
│
├── agent/                    # Windows senkronizasyon ajanı (SyncAdapter)
│   ├── src/
│   │   ├── SyncAdapter.Agent/         # Windows Service + sync worker
│   │   ├── SyncAdapter.Desktop/       # WinForms tray UI
│   │   ├── SyncAdapter.Core/          # Lokal sync state, retry logic
│   │   ├── SyncAdapter.Shared/        # Ajan tarafı ortak tipler
│   │   └── SyncAdapter.Infrastructure/# MSSQL client, HTTP client, Serilog
│   └── SyncAdapter.sln
│
├── admin-ui/                # (henüz boş) Super Admin React SPA — Faz 6
│
├── docs/
│   ├── 001-initial-schema.sql # Server DB şeması (PostgreSQL)
│   └── seed-admin.sql        # Admin tenant + admin API key
│
└── PLAN.md                   # Tüm plan, fazlama, kararlar
```

## Stack

- **Sunucu:** ASP.NET Core 8 Minimal API + EF Core 8 (Npgsql) + PostgreSQL 16
- **Multi-tenant:** PostgreSQL Row-Level Security (RLS) + EF Core interceptor
- **Auth:** `X-Api-Key` + `X-Tenant-Id` header (SHA-256 hash DB'de)
- **Ajan:** .NET 8 Worker Service (Windows Service olarak host) + Microsoft.Data.SqlClient
- **Logging:** Serilog (dosya + Windows EventLog + console)
- **HTTP retry:** Manuel exponential backoff (3 deneme: 1s/5s/30s)
- **Arayüz:** WinForms tray (NotifyIcon) — Faz 3'ün sonraki iterasyonu

## Hızlı Başlangıç (Geliştirme)

### 1) PostgreSQL'i ayağa kaldır

```powershell
cd server
docker compose up -d postgres
# Container healthy olunca:
Get-Content docs\..\docs\001-initial-schema.sql | docker exec -i fieldops-postgres psql -U fieldops -d fieldops
Get-Content docs\seed-admin.sql | docker exec -i fieldops-postgres psql -U fieldops -d fieldops
```

> **Not:** Windows + Docker Desktop + Npgsql kombinasyonunda development ortamında
> authentication quirk'leri olabilir. Production'da Linux PostgreSQL ile çalışır.

### 2) API'yi çalıştır

```powershell
cd server/src/FieldOps.Api
dotnet run
# http://localhost:5080
# Swagger: http://localhost:5080/swagger
```

### 3) Admin API key ile test et

`docs/seed-admin.sql` çalıştırıldıktan sonra **admin key**:
```
fo_live_rU4yGOT-lSWArqa87MMOB_u8UxsApjEFtVGYiAIjfyk
```

```powershell
$headers = @{ "X-Api-Key" = "fo_live_rU4yGOT-lSWArqa87MMOB_u8UxsApjEFtVGYiAIjfyk" }
Invoke-RestMethod "http://localhost:5080/api/v1/admin/tenants?page=1&pageSize=10" -Headers $headers
```

### 4) Windows ajanı

`agent/src/SyncAdapter.Agent/appsettings.json` içinde Mikro MSSQL bağlantı bilgileri
ve API base URL ayarlı. `dotnet run` ile başlatılabilir (development).

Üretim için Windows Service kurulumu:

```powershell
sc.exe create "FieldOps Agent" binPath="C:\Path\To\SyncAdapter.Agent.exe"
sc.exe start "FieldOps Agent"
```

## Endpoint Haritası

| Method | Path                                          | Kim?              | Açıklama |
|--------|-----------------------------------------------|-------------------|----------|
| GET    | /health                                       | Anonim            | API sağlık kontrolü |
| GET    | /api/v1/admin/tenants                         | Admin scope       | Tenant listesi |
| POST   | /api/v1/admin/tenants                         | Admin scope       | Yeni tenant oluştur |
| GET    | /api/v1/admin/tenants/{id}/keys               | Admin scope       | API key listesi |
| POST   | /api/v1/admin/tenants/{id}/keys               | Admin scope       | Yeni API key üret (düz metin sadece burada döner) |
| POST   | /api/v1/sync/push                             | Tenant scope      | Windows ajanı ERP'den batch gönderir |
| GET    | /api/v1/sync/state                            | Tenant scope      | Tabloların sync durumu |
| GET    | /api/v1/sync/runs                             | Tenant scope      | Son sync_run'lar |
| POST   | /api/v1/outbox/push                           | Tenant scope      | Android yeni belge push'lar |
| POST   | /api/v1/outbox/pull                           | Tenant scope      | Windows ajanı outbox'tan çeker |
| POST   | /api/v1/outbox/ack                            | Tenant scope      | Windows ajanı ERP yazma sonucunu bildirir |
| POST   | /api/v1/agent/events                          | Tenant scope      | Windows ajanı log event'lerini push'lar |
| GET    | /api/v1/data                                  | Tenant scope      | Hangi tablolar sync edilmiş |
| GET    | /api/v1/data/{tableName}                      | Tenant scope      | Tablodaki satırlar (Android için) |

## Faz Durumu

| Faz | İçerik | Durum |
|-----|--------|-------|
| F0  | Plan + onay | ✅ Tamam |
| F1  | Şema analizi (gerçek MikroDB dump) | ⏳ Beklemede |
| F2  | Server iskeleti (5 proje) | ✅ Tamam (build clean, şema DB'de) |
| F3  | Windows ajan iskeleti (5 proje) | ✅ Tamam (build clean, sync worker var) |
| F4  | Tüm tablolar + delta sync | ⏳ Sırada |
| F5  | Ters yön (outbox → ERP yazma) | ⏳ Sırada |
| F6  | Super Admin SPA (React) | ⏳ Sırada |
| F7  | Android kontrat paketi | ⏳ Sırada |
| F8  | E2E test + release | ⏳ Sırada |

Detaylı plan: bkz. [PLAN.md](./PLAN.md)
