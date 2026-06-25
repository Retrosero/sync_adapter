# FieldOps Bridge — Multi-Tenant Veri Köprüsü

**Durum:** Plan (onay bekliyor) · **Tarih:** 2026-06-25 · **Yazar:** Mavis

## Amaç

Türkiye'deki küçük/orta ölçekli şirketlerin Mikro ERP (v15) verilerini Android saha satış uygulamasına, **multi-tenant** ve **çift yönlü** olarak taşımak.

- **ERP → Sunucu:** MikroDB'deki 10 tablo + gerekli diğer tablolar, sürekli veya periyodik olarak sunucuya replike edilir. Sunucu tarafında tablo isimleri tıpatıp aynıdır, her satıra `tenant_id` eklenir.
- **Android → Sunucu → ERP:** Saha personelinin oluşturduğu yeni belgeler (sipariş, tahsilat vb.) Android'de outbox'a düşer, internete çıkınca sunucuya push'lanır. Windows ajanı sunucuyu sürekli dinler, yeni belgeleri Mikro'ya **idempotent + transaction** içinde yazar.
- **Super Admin:** Sunucuda ayrı bir SPA. Şirket kaydı → `tenant_id` + `api_key` üretimi. Android ve Windows ajanı bu kimlikle konuşur.

## Mimari Genel Bakış

```
┌─────────────────┐         ┌─────────────────────────┐         ┌─────────────────┐
│  Windows Ajan   │  pull   │   Sunucu (FieldOps API) │  pull   │  Android App    │
│  (C# Service +  │ ──────► │   .NET 8 Minimal API    │ ◄────── │  (Kotlin,       │
│   Tray + Outbox)│  push   │   PostgreSQL            │  push   │   outbox queue) │
│  ─ Mikro MSSQL  │         │   ─ tenant izolasyonu   │         │                 │
└─────────────────┘         └─────────────────────────┘         └─────────────────┘
                                       ▲
                                       │ admin API (CRUD tenant, key rotate)
                                       │
                              ┌─────────────────────────┐
                              │  Super Admin SPA        │
                              │  React + Vite + TS      │
                              └─────────────────────────┘
```

## Bileşenler

### 1. Windows Senkronizasyon Ajanı (C# / .NET 8)

- **Çalışma modu:** Windows Service (arka plan) + System Tray UI (kullanıcı kontrolü için, kapatabilsin ama service devam etsin).
- **Yapı:** `SyncAdapter` solution (mevcut Faz 1 iskeletinin genişletilmiş hali):
  - `SyncAdapter.Worker` — Windows Service host
  - `SyncAdapter.Desktop` — WinForms tray uygulaması
  - `SyncAdapter.Agent` — yeni: Mikro MSSQL bağlantısı + sunucu HTTP istemcisi
  - `SyncAdapter.Agent/Mikro/` — Mikro'ya özel SQL sorguları, change tracking
  - `SyncAdapter.Core` — outbox, idempotency, retry, logging
  - `SyncAdapter.Shared` — DTO'lar, ortak modeller
  - `SyncAdapter.Infrastructure` — EF Core, HTTP, config
  - `SyncAdapter.Security` — DPAPI, lisans kontrolü (opsiyonel, istersen kaldırırız)
- **Davranış:**
  - Her 30 sn'de bir sunucudan delta sync sinyali alır (POST /sync/pull).
  - Mikro'daki `ROWVERSION` veya `last_modified` kolonuna göre son başarılı sync'ten sonrasını çeker.
  - Gelen batch'i sunucuya POST /sync/push ile gönderir, cevapta hangi kayıtlar yazıldı bilgisini alır.
  - Ters yönde: sunucudaki outbox (Android'den gelen belgeler) POST /sync/erp-pull ile çeker, Mikro'ya yazar, sonucu POST /sync/erp-ack ile geri bildirir.
  - Tüm yazma işlemleri **transaction** içinde, **idempotency_key** ile (tekrar gelirse iki kez yazmaz).

### 2. Sunucu (FieldOps API)

- **Stack:** ASP.NET Core 8 Minimal API + EF Core (Npgsql) + PostgreSQL 16.
- **Modüller (tek solution):**
  - `FieldOps.Api` — endpoint'ler, auth middleware, rate limiting
  - `FieldOps.Domain` — entity'ler, enum'lar, validasyon
  - `FieldOps.Infrastructure` — EF Core DbContext, migration'lar, repository
  - `FieldOps.Application` — servisler, sync orkestrasyonu
  - `FieldOps.Contracts` — DTO'lar, Android ile ortak sözleşme
- **Auth:** Tüm isteklerde `X-Tenant-Id` + `X-Api-Key` header'ı. Hash'lenmiş api_key DB'de. Sabit bir request signing (HMAC) eklemeyi Faz 2'ye bırakıyoruz, baştaki ihtiyaç bu değil.
- **Multi-tenant izolasyon:** PostgreSQL **Row-Level Security (RLS)**. Her tabloya `tenant_id UUID NOT NULL`. EF Core interceptor ile session'a `app.current_tenant` set edilir, RLS policy'si bunu zorunlu kılar. Migration'lar bu policy'leri kurar. Yanlışlıkla başka tenant'ın verisini çekmek kod seviyesinde imkânsız hale gelir.

### 3. Veri Modeli (PostgreSQL)

- **Tablo isimleri tıpatıp MikroDB ile aynı**, küçük harfe çevrilmiş (PostgreSQL convention): `barkod_tanimlari`, `bankalar`, `cari_hesap_adresleri`, `cari_hesaplar`, `cari_hesap_hareketleri`, `stoklar`, `stok_hareketleri`, `kasalar`, `kasalar_yonetim`, `depolar`.
- Her tabloya:
  - `tenant_id UUID NOT NULL`
  - `synced_at TIMESTAMPTZ` (son sunucuya yazılma zamanı)
  - `source_modified_at TIMESTAMPTZ` (Mikro'daki son değişiklik)
  - `sync_batch_id UUID` (hangi batch'te geldi)
- **Ek tablolar (sistem):**
  - `tenants` — şirket kayıtları
  - `tenant_api_keys` — api_key hash + scope + last_used_at
  - `sync_runs` — her sync oturumunun logu (started, finished, counts, errors)
  - `outbox` — Android'den gelen yeni belgeler (status: pending/acked/failed, idempotency_key unique)
  - `android_devices` — cihaz kayıt (opsiyonel ama tavsiye)
- **Tablo listesi genişletme kararı:** İlk analizde belirtilen 10 tabloya ek olarak şunlar gerekli olabilir (kesinleşecek):
  - `stok_barkod_tanimlari` veya `barkod_tanimlari` zaten listede — ama `stok_birimleri` gerekebilir (satış birim dönüşümü için).
  - `cari_hesap_ozelkodlari` veya `cari_hesap_gruplari` (raporlama, filtreleme).
  - `stok_aliyorlar`, `stok_gruplari` (stok hiyerarşisi).
  - `iller`, `ilceler`, `ulkeler` (adres için).
  - `kdv_oranlari` (vergi hesabı).
  - `doviz_kurlari` (TL dışı satışlar).
  - `belge_numara_tipleri` veya `evrak_numaralari` (Mikro'nun dahili evrak seri takibi — Android'den belge gönderirken lazım).
  - `cari_hesap_hareketleri` zaten listede ama yanında `odeme_Planlari` veya `vade_takvimi` gerekebilir.

  **Final liste tünel sonrası netleşecek.** Schema dump gelince her tablonun kolon + index + FK yapısını analiz edip "Android'de gerçekten neye ihtiyaç var" sorusunu cevaplayacağız, gerekli olan minimum seti seçeceğiz.

### 4. Super Admin SPA

- **Stack:** React 18 + Vite + TypeScript + TanStack Query + react-hook-form.
- **Repo:** `fieldops-admin/`, ayrı paket.
- **Sayfalar (MVP):**
  - Login (super admin şifresi — environment'tan okunur, DB'de değil)
  - Tenants listesi (CRUD)
  - Tenant detay: api_key üret, rotate et, son kullanım tarihleri
  - Sync run logları (hangi Windows ajanı ne zaman ne yaptı, hata var mı)
- **Build çıktısı:** SPA build → statik dosyalar → `fieldops-admin/dist/`. Sunucu tarafında `FieldOps.Api` bu dosyaları `/admin/*` route'unda serve eder. Ayrı hosting yok, tek deployment.

### 5. Android Entegrasyonu (bu turda kod yok, kontratı çıkarıyoruz)

- Android uygulaması, kurulum sırasında Super Admin'den aldığı `tenant_id` + `api_key` ikilisini cihazda güvenli saklar.
- `X-Tenant-Id` + `X-Api-Key` header ile sunucuya konuşur.
- Yeni belge (sipariş, tahsilat) → önce cihazdaki SQLite outbox'a yazılır, sonra `POST /api/v1/sync/push-outbox` ile sunucuya gönderilir. Cevapta `idempotency_key` ile ACK gelirse Android o kaydı outbox'tan siler.
- Sunucu, gelen belgeyi `outbox` tablosuna yazar, Windows ajanı oradan alıp Mikro'ya yazar.
- Bu kontrat (DTO'lar, endpoint şeması, hata kodları) `FieldOps.Contracts` içinde tanımlanır; Android ekibi Kotlin tarafında bunu tüketir.

## Senkronizasyon Protokolü (Detay)

### ERP → Sunucu (Windows ajanı tarafı)

1. Ajan başlarken son başarılı `sync_runs` kaydının `checkpoint` değerini okur (son çektiği `ROWVERSION` veya timestamp).
2. Mikro'ya `SELECT ... FROM tablo WHERE rowversion > @checkpoint` sorgusu atar (her tablo için).
3. Gelen satırları batch'lere böler (örn. 1000 satır/batch), sunucuya `POST /api/v1/sync/push` ile gönderir. Body: `{ table, tenantId, batchId, rows: [...] }`.
4. Sunucu, gelen batch'i ilgili PostgreSQL tablosuna yazar. Aynı `sync_batch_id` ile auditlenir.
5. Sunucu 200 dönünce ajan checkpoint'i ilerletir, bir sonraki tabloya geçer.
6. Hata durumunda: 3 kere exponential backoff ile tekrar dener, sonra `sync_runs` tablosuna `failed` yazar ve admin SPA'dan görünür.

### Sunucu → Android (Android tarafı)

- Android periyodik olarak `POST /api/v1/sync/pull?since=<timestamp>` çağırır. Sunucu, Android'in son çektiği andan sonra değişen kayıtları döner. Bu sayede Android her zaman kendi DB'sini güncel tutar.

### Android → Sunucu → ERP (ters yön)

1. Android, yeni belgeyi lokal outbox'a yazar: `{ idempotency_key, payload, created_at }`. `idempotency_key` cihazda UUID üretir, tek seferliktir.
2. İnternet varken `POST /api/v1/sync/push-outbox` ile payload'u sunucuya gönderir.
3. Sunucu, `outbox` tablosuna yazar (idempotency_key UNIQUE constraint → tekrar gelirse 200 döner ama yeni insert yapmaz, ACK verir).
4. Windows ajanı sürekli `GET /api/v1/sync/erp-pull?limit=50` çağırır. Pending outbox kayıtlarını çeker.
5. Her belgeyi Mikro'da uygun stored procedure veya INSERT'e çevirip transaction içinde yazar. Başarılıysa sunucuya `POST /api/v1/sync/erp-ack { idempotency_key, erp_ref }` ile bildirir.
6. Sunucu outbox kaydını `acked` yapar. Bir sonraki Android pull'unda Android o ACK'i alır ve lokal outbox'tan siler.
7. **Güvenlik:** Yazma sadece bu outbox → agent → ERP yolundan geçer. ERP'ye ajan dışında hiçbir şey yazmaz. Tüm işlemler idempotent.

## Veri İzolasyonu — Neden RLS

- `tenant_id` her tablonun kolonunda var, ama sorgu yazarken `WHERE tenant_id = @current` unutulursa felaket. EF Core interceptor'la bunu zorla yaptırabiliriz ama her yeni geliştirici için tekrar tekrar savaşmak yerine PostgreSQL'in RLS özelliğini kullanacağız.
- Migration'da: `ALTER TABLE cari_hesaplar ENABLE ROW LEVEL SECURITY;` + `CREATE POLICY tenant_isolation ON cari_hesaplar USING (tenant_id = current_setting('app.current_tenant')::uuid);`
- Connection açılırken `SET app.current_tenant = '<guid>';` çalıştırılır (Npgsql interceptor).
- Super admin endpoint'leri RLS'yi bypass eder (connection'da `BYPASSRLS` rolü ile), böylece tüm tenant'ları görebilir.

## Faz Planı

Bu plan **tek oturumda bitmeyecek**. Aşamalar:

| Faz | İçerik | Süre (tahmini) |
|---|---|---|
| **F0 — Onay** | Bu planı onayla, tünel kurulumu netleşsin | bu oturum |
| **F1 — Şema analizi** | Tünel/Dump → MikroDB şemasını çek, mapping dokümanı yaz, gerekli tablo listesi final | 0.5–1 gün |
| **F2 — Sunucu iskeleti** | .NET solution kur, EF Core + PostgreSQL, tenants/api_keys/sync_runs/outbox tabloları, RLS policy'leri, auth middleware | 1 gün |
| **F3 — Windows ajan iskeleti** | SyncAdapter çözümünü kur, MSSQL bağlantısı, ilk tablo için ERP→Sunucu pull/push çalışsın | 1–2 gün |
| **F4 — Tüm tablolar** | Belirlenen tüm tabloları sırayla ekle, delta sync doğru çalışsın | 1–2 gün |
| **F5 — Ters yön (outbox)** | Android outbox endpoint'i + Windows ajan ERP yazma katmanı + transaction/idempotency testleri | 1–2 gün |
| **F6 — Super Admin SPA** | React app, tenant CRUD, api_key rotate, sync logları | 1 gün |
| **F7 — Android kontrat** | `FieldOps.Contracts` paketi, DTO'lar, örnek Kotlin tüketicisi (mock) | 0.5 gün |
| **F8 — E2E test + release** | Bir gerçek tenant ile uçtan uca test, hata düzeltme, release notes | 1 gün |

> Her faz sonunda çalışan, doğrulanabilir bir çıktı var. F1 tamamlanmadan F2'ye geçmeyiz, çünkü tüm mapping kararları F1'de netleşiyor.

## Kritik Dış Bağımlılık

### MikroDB'ye bağlantı — çözülmesi gereken

Senin seçimin "remote_conn" oldu, ama MikroDB **Windows Authentication** kullanıyor. TCP tüneli (cloudflared/ngrok) sadece ağ katmanını aşar; auth yine Windows tarafında. Yani tünel + Windows Auth ile ben senin AD credential'ınla login olmam gerekir — bunu bana vermeni istemem, gerek de yok.

**Üç seçenek, birini seç:**

1. **Mixed Mode + geçici read-only kullanıcı (önerim).** SQL Server'da mixed mode auth aç, bana sadece şema görme yetkisi olan bir login oluştur (`SELECT` + `VIEW DEFINITION`). Tünel üzerinden ben bağlanırım, sadece metadata okurum, hiçbir veriyi dışarı çekmem. İşim bitince login'i silersin.
2. **Anonymized schema dump (en güvenli).** Sen şu komutu çalıştırırsan: `sqlcmd -S GURBUZ -d MikroDB_V15_02 -E -Q "EXEC sp_helpdb 'MikroDB_V15_02'" > schema_meta.sql` ve tablo listesini al, sonra her tablo için `sp_help` + `sp_helpindex` + `sp_helpconstraint` çıktısını bana at. Veri yok, sadece yapı. Ben mapping'i bu çıktılardan çıkarırım.
3. **Full schema script (en pratik).** SSMS'te: `Tasks → Generate Scripts → Script entire database and all database objects → Advanced → Types of data to script = Schema only` → bana .sql dosyası at. 50-200 KB civarı olur, sıkıntı yok.

Benim tahminim **3. seçenek** en hızlısı ve en temizi. Hazır SSMS açtığında 2 dakikada üretirsin, bana at, ben analiz edeyim. Ama sen karar ver.

Eğer illa tünel kurmak istersen 1. seçeneği yapalım, 5-10 dakikalık bir setup.

## Kabul Kriterleri (F8 sonunda)

- [ ] Windows ajanı bir şirketin Mikro'suna bağlanıp 10+ tabloyu sunucuya replike edebiliyor.
- [ ] Sunucu, gelen verileri tenant bazında izole ediyor (RLS bypass denemeleri 0 satır döner).
- [ ] Super Admin'den yeni bir tenant oluşturulunca api_key üretiliyor, Android ve Windows ajanı bu key ile auth olabiliyor.
- [ ] Android'den gelen bir sipariş belgesi: outbox → sunucu → Windows ajanı → Mikro INSERT → ACK. Tekrar gönderilirse Mikro'da duplicate oluşmuyor.
- [ ] Ajan, sunucu 5 dakika kapalı kalsa bile kuyruktaki işleri sırayla, kayıpsız işliyor.
- [ ] Tüm yazma işlemleri transaction içinde, partial failure durumunda rollback.

## Şu Anki Kararlar — Özet

| Karar | Seçim |
|---|---|
| Sunucu stack | .NET 8 Minimal API + EF Core |
| Sunucu DB | PostgreSQL 16 |
| Admin UI | Ayrı React + Vite SPA |
| Şema kaynağı | Connection string verildi (aşağıya bak), uygulama buna göre bağlanacak |
| Tablo isimleri | MikroDB ile tıpatıp aynı (PostgreSQL: küçük harf) |
| Multi-tenant izolasyon | PostgreSQL RLS + EF Core interceptor |
| Auth | `X-Tenant-Id` + `X-Api-Key` header (HMAC signing Faz 2'de) |
| Windows ajan mimarisi | Windows Service + System Tray |
| ERP yazma yolu | Sadece outbox → ajan → ERP (güvenli) |
| Android kontrat | `FieldOps.Contracts` ortak DTO paketi |
| Sync stratejisi | **Delta + bootstrap**: karşı tablo boşsa full pull, doluysa checkpoint'ten devam |
| Loglama | Serilog (structured) → dosya + Windows Event Log + sunucuya push |
| Hata yönetimi | Retry + exponential backoff + dead-letter queue + admin SPA'dan görünür |

## MSSQL Bağlantı Konfigürasyonu (Serhan'dan geldi)

`SyncAdapter.Agent/appsettings.json` içine yerleştirilecek:

```json
{
  "Mikro": {
    "Server": "GURBUZ",
    "Port": 1433,
    "Database": "MikroDB_V15_02",
    "UseWindowsAuth": true,
    "User": "",
    "Password": "",
    "Encrypt": false,
    "TrustServerCertificate": true,
    "CommandTimeoutSeconds": 300,
    "PoolSize": 10
  }
}
```

Connection string oluşturma: `Server=GURBUZ,1433;Database=MikroDB_V15_02;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;`

> Windows Service, ERP sunucusuna erişimi olan bir Windows kullanıcısı (tercihen domain service account) olarak çalışmalı. Trusted_Connection bu yüzden çalışacak. SQL Auth'a geçmek istersek `UseWindowsAuth=false` + `User` + `Password` doldurulur.

## Delta Sync + Bootstrap Kuralı

Her tablo için `sync_state` adında bir kontrol tablosu tutulacak (sunucuda):

```
sync_state (
  table_name      TEXT PRIMARY KEY,
  last_run_at     TIMESTAMPTZ,
  last_status     TEXT,            -- ok | failed | in_progress
  rows_synced     BIGINT,
  checkpoint_ts   TIMESTAMPTZ,     -- Mikro'daki son değişiklik timestamp
  checkpoint_rv   BYTEA,           -- opsiyonel: ROWVERSION değeri
  is_initial      BOOLEAN           -- true ise sonraki sync full pull
)
```

**Mantık (Windows ajanı tarafında, her tablo için):**

1. `sync_state` satırını oku.
2. Eğer `last_status = failed` ve retry sayısı >= max_retry → dead-letter'e at, admin'e bildir, dur.
3. Eğer karşı (sunucu) tabloda ilgili tenant için hiç satır yoksa (`COUNT(*) = 0`):
   - `is_initial = true` yap.
   - Mikro'dan **full pull** (sayfalama ile, batch 5000 satır).
4. Eğer satır varsa ve `is_initial = false`:
   - **Delta pull**: `WHERE last_modified > @checkpoint_ts` (veya `WHERE rowversion > @checkpoint_rv`).
5. Batch'ler halinde sunucuya POST.
6. Tüm batch'ler başarılı olunca `checkpoint_ts`/`rv`'yi güncelle, `last_status = ok`.
7. Herhangi bir batch fail olursa: tüm transaction'ı geri al, `last_status = failed` + `retry_count++`, sonraki tick'te tekrar dene.

## Gelişmiş Log + Hata Yönetimi

### Loglama katmanları

1. **Serilog** — yapılandırılmış JSON loglar. Her event'te:
   - `tenant_id`, `table_name`, `batch_id`, `run_id`
   - `level` (Verbose/Debug/Info/Warning/Error/Fatal)
   - `exception` (varsa)
   - `elapsed_ms`, `rows_affected`
2. **Sink'ler (hepsi birden aktif):**
   - `Serilog.Sinks.File` — günlük rotasyon, 30 gün tut, sıkıştır (zip)
   - `Serilog.Sinks.EventLog` — Windows Event Log, sadece Warning+ (Source: `FieldOps.Agent`)
   - `Serilog.Sinks.Console` — debug için
   - **Custom HTTP sink** — önemli event'leri sunucuya push'la (`POST /api/v1/agent/events`), admin SPA canlı görsün
3. **Dosya yapısı:**
   ```
   C:\ProgramData\FieldOps\Agent\Logs\
     fieldops-agent-2026-06-25.log
     fieldops-agent-2026-06-24.log.gz
     ...
   ```
4. **PII / secret filtreleme:** Password, api_key, connection string otomatik redact edilir.

### Hata yönetimi yapısı

- **Retry policy:** Polly ile. Üst seviye: 3 deneme, exponential backoff (1s, 5s, 30s). Alt seviye (her batch): 2 deneme, 2s + 10s.
- **Dead-letter queue:** Bir tablo 5 ardışık denemede de fail olursa, o tablo için sync durur, `sync_state.dead_lettered = true` yapılır. Admin SPA'dan manuel "retry" butonu olacak.
- **Hata kategorileri:**
  - `TransientError` → otomatik retry (network, timeout, deadlock)
  - `SchemaError` → retry yok, alarm (kolon uyumsuzluğu, tablo yok)
  - `AuthError` → retry yok, alarm (Windows Auth fail, sql login expire)
  - `DataError` → o batch'i atla, logla, devam et (örn. invalid char, constraint violation)
- **Alarm mekanizması:** Warning+ seviye log sunucuya gider, sunucu admin'e (ileride e-posta/webhook) iletir. Şimdilik sadece admin SPA'da görünür.

### Kontrol

Ajan üzerindeki tüm state, log ve konfigürasyon:
- **Lokal:** `C:\ProgramData\FieldOps\Agent\` altında config, log, queue dosyaları
- **Sunucu:** Admin SPA'dan ajanın son kalp atışı, son hataları, sync_run geçmişi görünür
- **API:** `GET /api/v1/admin/agents/{agentId}/health` endpoint'i ile programatik erişim
- **CLI:** Ajan kendi içinde `--status`, `--logs`, `--retry-failed` flag'leri sunar (tray app'ten erişilebilir)

## Faz Planı (güncellenmiş)

| Faz | İçerik |
|---|---|
| **F1 — Şema analizi** | MikroDB'den ilk 10 tablo + gerekli diğer tabloları analiz, `schema-mapping.md` + `gerekli-tablolar.md` yaz. Serhan'ın verdiği connection ile SSMS erişimi gerekirse dump iste. |
| **F2 — Sunucu iskeleti** | .NET solution, EF Core + PostgreSQL, tenants/api_keys/sync_runs/outbox/sync_state tabloları, RLS, auth middleware, gelişmiş log altyapısı |
| **F3 — Windows ajan iskeleti** | SyncAdapter çözümü, Windows Service + Tray, MSSQL bağlantısı, Serilog + retry + dead-letter, ilk tablo için ERP→Sunucu pull/push uçtan uca |
| **F4 — Tüm tablolar + delta sync** | 10+ tablo için delta+bootstrap, paralel sync orchestrator |
| **F5 — Ters yön (outbox)** | Android outbox endpoint'i + Windows ajan ERP yazma katmanı + transaction/idempotency testleri |
| **F6 — Super Admin SPA** | React app, tenant CRUD, api_key rotate, sync logları, agent health, dead-letter retry UI |
| **F7 — Android kontrat** | `FieldOps.Contracts` paketi, DTO'lar, örnek Kotlin tüketicisi (mock) |
| **F8 — E2E test + release** | Bir gerçek tenant ile uçtan uca test, hata düzeltme, release notes |

## Sonraki Adım

**Bu oturumda F2 (sunucu iskeleti) + F3 (Windows ajan iskeleti) başlıyorum.** Şema analizini F4 öncesi hallederiz. Şimdilik genel tablo yapısı (tenant_id, source_modified_at, vs.) ile başlıyoruz, kolon isimleri F1 sonrası netleşir.

Eğer "şema analizini bekle, F2'ye geçme" dersen F1 ile devam ederim. Ama bu oturumda görünür bir şey çıksın istiyorsan F2 ile başlamayı öneriyorum.
