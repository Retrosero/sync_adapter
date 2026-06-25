# FieldOps REST API — Android Entegrasyon Rehberi

**Versiyon:** 0.1.0 · **Durum:** Taslak · **Tarih:** 2026-06-25

---

## Genel Bakış

FieldOps üç katmanlı bir veri köprüsüdür:

```
┌─────────────────┐    push     ┌──────────────────────┐   pull    ┌──────────────┐
│  Android App     │ ──────────►│  FieldOps API        │ ─────────►│ Windows Ajan │
│  (Kotlin/SQLite) │            │  (ASP.NET Core 8)   │           │  (ERP push)  │
└─────────────────┘            └──────────────────────┘           └──────────────┘
         ▲                            │                               │
         │         outbox push        │         erp write + ack      │
         └─────────────────────────────┘                               │
                                                                            ▼
                                                                    ┌──────────────┐
                                                                    │  Mikro ERP    │
                                                                    │  (MSSQL)      │
                                                                    └──────────────┘
```

**Sync yönleri:**
1. **ERP → Server → Android:** Windows ajanı MikroDB'yi okur → sunucuya push eder → Android sunucudan çeker
2. **Android → Server → ERP:** Android outbox'a yazar → sunucuya push eder → Windows ajan sunucuyu okur → Mikro'ya yazar

---

## Kimlik Doğrulama

Tüm istekler `X-Tenant-Id` ve `X-Api-Key` header'ı gerektirir.

```
X-Tenant-Id: <UUID>           # Ör: "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
X-Api-Key: <plain string>      # Ör: "fo_live_rU4yGOT-..."
```

> API key Super Admin panelinden üretilir. **Düz metin olarak saklanır** —
> Android Keystore veya EncryptedSharedPreferences kullanın.

Başarısız auth: `401 Unauthorized`

---

## Endpoint'ler

### 1. Outbox Push — `POST /api/v1/outbox/push`

Android'in sunucuya yeni belge göndermesi.

**Request:**
```json
{
  "tenantId": "a1b2c3d4-...",
  "deviceId": "android-xyz",
  "items": [
    {
      "idempotencyKey": "550e8400-e29b-41d4-a716-446655440000",
      "documentType": "sales_order",
      "payload": {
        "cari_kod": "C001",
        "siparis_tarihi": "2026-06-25",
        "gonderim_tarihi": "2026-06-28",
        "satir_count": 2,
        "satirlar": [
          { "stok_kod": "S001", "miktar": 10, "birim_fiyat": 150.00, "kdv_orani": 20 }
        ]
      },
      "createdAt": "2026-06-25T10:30:00Z"
    }
  ]
}
```

**Response (200 OK):**
```json
{
  "accepted": [
    {
      "idempotencyKey": "550e8400-e29b-41d4-a716-446655440000",
      "serverId": "b2c3d4e5-...",
      "status": "Pending",
      "acceptedAt": "2026-06-25T10:30:01Z"
    }
  ],
  "rejected": []
}
```

**İmportant:** Aynı `idempotencyKey` ile tekrar gönderilirse sunucu **HTTP 200** döner ama yeni insert **yapmaz**. ACK yine gelir. Bu mekanizma outbox push'un idempotent olmasını sağlar.

**Hata kodları:**
- `400 Bad Request` — validation hatası
- `401 Unauthorized` — geçersiz API key veya pasif tenant
- `500 Internal Server Error` — sunucu hatası

---

### 2. Data Pull — `GET /api/v1/data/{tableName}`

Android'in sunucudan tablo verisi çekmesi.

**Query parametreleri:**
| Param | Tip | Açıklama |
|-------|-----|----------|
| `since` | string (ISO-8601 UTC) | Opsiyonel. Sadece bu zamandan sonra sync edilmiş kayıtları döner. İlk sync için atlanır. |
| `page` | int | 1-indexed. Varsayılan: 1 |
| `pageSize` | int | Max 1000. Varsayılan: 200 |

**Örnek:** `GET /api/v1/data/cari_hesaplar?since=2026-06-24T00:00:00Z&page=1&pageSize=200`

**Response:**
```json
{
  "tableName": "cari_hesaplar",
  "total": 1500,
  "page": 1,
  "pageSize": 200,
  "rows": [
    {
      "id": "rec_abc123",
      "tableName": "cari_hesaplar",
      "sourcePk": "C001",
      "payload": {
        "cari_kod": "C001",
        "cari_ad": "Gürbüz Oyuncak Tic.",
        "vergi_dairesi": "Kadıköy",
        "vergi_no": "1234567890",
        "telefon": "0216 555 1234",
        "adres": "Caferağa Mah. Moda Cad. No:12 Kadıköy/İstanbul",
        "bakiye": 12500.00,
        "aktif": true
      },
      "sourceModifiedAt": "2026-06-24T08:15:00Z",
      "syncedAt": "2026-06-24T08:15:30Z"
    }
  ]
}
```

**Satır okuma (Kotlin örnek):**
```kotlin
val cariKod = (row.payload["cari_kod"] as? kotlinx.serialization.json.JsonPrimitive)?.content
val cariAd = (row.payload["cari_ad"] as? kotlinx.serialization.json.JsonPrimitive)?.content
val bakiye = (row.payload["bakiye"] as? kotlinx.serialization.json.JsonPrimitive)?.content?.toDoubleOrNull()
```

---

### 3. Table List — `GET /api/v1/data/`

Sunucudaki tüm tabloların listesini döner. Android sync başlamadan önce kontrol için kullanılabilir.

**Response:**
```json
[
  { "tableName": "cari_hesaplar",     "count": 1250, "lastSyncedAt": "2026-06-25T08:00:00Z" },
  { "tableName": "stoklar",           "count": 3400, "lastSyncedAt": "2026-06-25T08:01:00Z" },
  { "tableName": "barkod_tanimlari",  "count": 5200, "lastSyncedAt": "2026-06-25T08:02:00Z" },
  { "tableName": "cari_hesap_hareketleri", "count": 45000, "lastSyncedAt": "2026-06-25T08:03:00Z" }
]
```

---

### 4. Health — `GET /health`

Sunucu erişilebilirlik kontrolü. Auth gerektirmez.

**Response:** `200 OK { "status": "ok", "time": "2026-06-25T08:00:00Z" }`

---

## Belge Türleri (documentType)

| documentType | Açıklama | Zorunlu Alanlar |
|---|---|---|
| `sales_order` | Satış siparişi | `cari_kod`, `siparis_tarihi`, `satirlar` |
| `collection` | Tahsilat | `cari_kod`, `tarih`, `tutar`, `odeme_sekli` |
| `payment` | Ödeme (cariden -> firmaya) | `cari_kod`, `tarih`, `tutar` |

**Gelecekte:** `stock_movement`, `return`, `price_update`

---

## Tablo Listesi (MikroDB → PostgreSQL)

> **F1 sonrası kesinleşecek.** Şu anki tahmin:

| MikroDB Tablo | PostgreSQL Tablo | Açıklama |
|---|---|---|
| CARI_HESAPLAR | `cari_hesaplar` | Cari hesap kartları |
| STOKLAR | `stoklar` | Stok kartları |
| BARKOD_TANIMLARI | `barkod_tanimlari` | Barkod-ürün eşleştirmesi |
| CARI_HESAP_ADRESLERI | `cari_hesap_adresleri` | Cari adresleri |
| CARI_HESAP_HAREKETLERI | `cari_hesap_hareketleri` | Cari hareketleri (borç/alacak) |
| STOK_HAREKETLERI | `stok_hareketleri` | Stok hareketleri |
| KASALAR | `kasalar` | Kasa kartları |
| KASALAR_YONETIM | `kasalar_yonetim` | Kasa işlemleri |

---

## Offline-First Stratejisi

### Outbox Akışı

```
1. Android: Yeni sipariş oluştur
   ↓
2. Lokal SQLite outbox'a yaz:
   { idempotency_key: UUID, payload, created_at, status: "pending" }
   ↓
3. İnternet var mı kontrol et
   ↓
4. Varsa → POST /outbox/push
   5a. 200 + accepted → local outbox'dan SIL
   5b. 5xx / timeout → local outbox'da TUT (retry later)
   5c. 4xx → hata göster, retry etme
   ↓
6. Periyodik WorkManager (örn. 15 dk) outbox'taki pending item'ları push eder
```

### Data Sync Akışı

```
1. Uygulama açılır → WorkManager sync tetikler
   ↓
2. Her tablo için:
   a. Son checkpoint'i SQLite'dan oku
   b. GET /data/{tableName}?since=<checkpoint>
   c. Gelen satırları SQLite'a upsert et (ON CONFLICT UPDATE)
   d. En son syncedAt → checkpoint olarak kaydet
   ↓
3. Tüm checkpoint'leri SQLite'a yaz
   ↓
4. İkinci açılışta kaldığı yerden devam
```

### İdempotentlik Kuralları

1. **Outbox push:** `idempotencyKey` = UUID. Aynı key tekrar gönderilse bile sunucu duplicate insert etmez.
2. **Data upsert:** `sourcePk` (MikroDB PK) kullanılarak INSERT OR REPLACE yapılır — aynı kayıt tekrar gelirse güncellenir.
3. **ERP write (Windows ajan):** Her outbox item idempotency_key ile Mikro'ya yazılır. Mikro tarafında kontrol mevcuttur (evrak no unique constraint).

---

## Retry Policy

| Durum | Android Davranışı |
|---|---|
| Timeout / 5xx | Exponential backoff: 1s → 5s → 30s. 3 deneme sonra beklet. |
| 4xx (validation) | Hata göster, kullanıcıya bildir, retry etme. |
| 401 Unauthorized | API key geçersiz → kullanıcıyı auth ekranına yönlendir. |

---

## Güvenlik Notları

- **API key** düz metin olarak saklanmamalı. Android 6.0+ için EncryptedSharedPreferences kullanın.
- **Network:** HTTPS zorunlu. Sunucu sertifikası pinned edilmeli (Certificate Pinning).
- **Log:** Payload içeriği log'lanmamalı — müşteri verisi içerebilir.
- **Mikro'ya yazma:** Android asla doğrudan Mikro'ya yazmaz. Sadece sunucu → Windows ajan → Mikro yolu kullanılır.

---

## Kullanılan Teknolojiler

- **HTTP:** OkHttp 4.x
- **JSON:** kotlinx-serialization 1.6+
- **Async:** Kotlin Coroutines
- **Lokal DB:** Android SQLite / Room
- **Background:** WorkManager (periodic sync)
