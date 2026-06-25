# MikroDB V15_02 — Şema Analiz Raporu

**Tarih:** 2026-06-25
**Veritabanı:** `GURBUZ / MikroDB_V15_02`
**Bağlantı:** Windows Authentication (Trusted_Connection)

> ⚠️ **Okuma only — hiçbir veri değiştirilmedi veya silinmedi.**

---

## Genel Bakış

- **Toplam tablo sayısı:** 391 tablo (`dbo` şeması)
- **Sunucu:** `GURBUZ` (Windows makine adı)
- **Mimari Notu:** Her tabloda standart Mikro alanları: `*_RECno`, `*_RECid_DBCno`, `*_RECid_RECno`,
  `*_degisti`, `*_hidden`, `*_iptal`, `*_kilitli`, `*_lastup_date`, `*_create_date`

---

## Delta Sync İçin Kritik Sütunlar

Mikro'nun tüm tablolarında `*_lastup_date` sütunu vardır — bu sütun **değişiklik takibi için
kullanılır.** ROWVERSION/timestamp kullanılmaz.

```
Mikro tablosu  →  * _lastup_date
CARI_HESAPLAR  →  cari_lastup_date
STOKLAR        →  sto_lastup_date
SIPARISLER     →  sip_lastup_date
CARI_HESAP...  →  cha_lastup_date
STOK_HAREKET.. →  sth_lastup_date
KASALAR        →  kas_lastup_date
BARKOD_TANIM.. →  bar_lastup_date
```

---

## Tablo Detayları

### 1. CARI_HESAPLAR — Cari/Müşteri Hesapları

| Alan | Tip | Açıklama |
|------|-----|----------|
| `cari_RECno` | int | **Primary key** (internal record no) |
| `cari_kod` | nvarchar(25) | Cari kod (kullanıcı görür) |
| `cari_unvan1` | nvarchar(50) | Cari unvan |
| `cari_unvan2` | nvarchar(50) | 2. unvan |
| `cari_hareket_tipi` | tinyint | 0=Müşteri, 1=Tedarikçi, 2=Müşteri&Tedarikçi |
| `cari_vdaire_adi` | nvarchar(50) | Vergi dairesi |
| `cari_VergiKimlikNo` | nvarchar(10) | VKN / TCKN |
| `cari_EMail` | nvarchar(80) | E-posta |
| `cari_CepTel` | nvarchar(20) | Cep telefonu |
| `cari_adres_no` | int | Adres tablosuna FK |
| `cari_sat_fk` | int | Satış fiyat listesi FK |
| `cari_lastup_date` | datetime | **Son güncelleme** |
| `cari_kilitli` | bit | Kilitli mi? |
| `cari_iptal` | bit | İptal mi? |

**Satır sayısı:** 513
**Son değişiklik:** 2026-06-22 14:01:42

---

### 2. STOKLAR — Stok Tanımları

| Alan | Tip | Açıklama |
|------|-----|----------|
| `sto_RECno` | int | **Primary key** |
| `sto_kod` | nvarchar(25) | Stok kodu |
| `sto_isim` | nvarchar(50) | Stok ismi |
| `sto_kisa_ismi` | nvarchar(25) | Kısa isim |
| `sto_cins` | tinyint | Cins (tip) |
| `sto_birim1_ad` | nvarchar(10) | Birim 1 adı |
| `sto_birim2_ad` | nvarchar(10) | Birim 2 adı |
| `sto_doviz_cinsi` | tinyint | Para birimi |
| `sto_uretric_kodu` | nvarchar(25) | Üretici kodu |
| `sto_marka_kodu` | nvarchar(25) | Marka kodu |
| `sto_kategori_kodu` | nvarchar(25) | Kategori |
| `sto_yer_kod` | nvarchar(25) | Reyon/lokasyon |
| `sto_lastup_date` | datetime | **Son güncelleme** |
| `sto_pasif_fl` | bit | Pasif mi? |
| `sto_tasfiyede` | bit | Tasfiye mi? |

**Satır sayısı:** 4,446
**Son değişiklik:** 2026-06-25 14:43:31

---

### 3. SIPARISLER — Satış Siparişleri (Satır Bazlı)

| Alan | Tip | Açıklama |
|------|-----|----------|
| `sip_RECno` | int | **Primary key** |
| `sip_tarih` | datetime | Sipariş tarihi |
| `sip_teslim_tarih` | datetime | Teslim tarihi |
| `sip_evrakno_seri` | nvarchar(6) | Evrak seri |
| `sip_evrakno_sira` | int | Evrak sıra no |
| `sip_musteri_kod` | nvarchar(25) | Cari kod (CARI_HESAPLAR FK) |
| `sip_stok_kod` | nvarchar(25) | Stok kodu (STOKLAR FK) |
| `sip_miktar` | float | Miktar |
| `sip_tutar` | float | Tutar |
| `sip_vergi` | float | Vergi tutarı |
| `sip_vergi_pntr` | tinyint | Vergi oranı pointer |
| `sip_doviz_cinsi` | tinyint | Para birimi |
| `sip_doviz_kuru` | float | Döviz kuru |
| `sip_tip` | tinyint | Sipariş tipi |
| `sip_cins` | tinyint | Cinsi |
| `sip_durumu` | tinyint | Durum |
| `sip_lastup_date` | datetime | **Son güncelleme** |
| `sip_iptal` | bit | İptal mi? |

**Satır sayısı:** 1 (sistemde şu an sadece 1 sipariş satırı var — 2026-06-20)
**Son değişiklik:** 2026-06-20 09:27:49

> 📝 **Not:** SIPARISLER satır bazlıdır — her sipariş kalemi ayrı satırdır.
> SIPARISLER_OZET ayrı bir özet tablosudur (sipariş header bilgisi).

---

### 4. CARI_HESAP_HAREKETLERI — Cari Hareketleri (Borç/Alacak/Tahsilat)

| Alan | Tip | Açıklama |
|------|-----|----------|
| `cha_RECno` | int | **Primary key** |
| `cha_evrak_tip` | tinyint | Evrak tipi |
| `cha_tip` | tinyint | 0=Borç, 1=Alacak |
| `cha_cinsi` | tinyint | Cins |
| `cha_tarihi` | datetime | İşlem tarihi |
| `cha_kod` | nvarchar(25) | Cari kod (CARI_HESAPLAR FK) |
| `cha_karsidcinsi` | tinyint | Karşı cins |
| `cha_karsid_kur` | float | Karşı kur |
| `cha_miktari` | float | Miktar |
| `cha_meblag` | float | **Tutar (TL)** |
| `cha_vade` | int | Vade gün |
| `cha_belge_no` | nvarchar(20) | Belge no |
| `cha_belge_tarih` | datetime | Belge tarihi |
| `cha_aciklama` | nvarchar(40) | Açıklama |
| `cha_evrakno_seri` | nvarchar(6) | Evrak seri |
| `cha_evrakno_sira` | int | Evrak sıra |
| `cha_vergi_pntr` | tinyint | KDV oranı pointer |
| `cha_vergi1..10` | float | Vergi tutarları |
| `cha_lastup_date` | datetime | **Son güncelleme** |
| `cha_iptal` | bit | İptal mi? |
| `cha_uuid` | nvarchar(40) | Benzersiz UUID |

**Evrak tipi dağılımı (toplam 12,133 satır):**
| evrak_tip | Açıklama | Satır |
|---|---|---|
| 0 | Kar.Çek (Müşteri çeki) | 898 |
| 1 | Faturalar | 1,969 |
| 29 | Müşteri çeki | 71 |
| 37 | Kredi kartı | 151 |
| 63 | **KASA tahsilat** (nakit) | 8,418 |
| 64 | Havale/EFT | 625 |

**Son değişiklik:** 2026-06-25 15:10:22

---

### 5. STOK_HAREKETLERI — Stok Hareketleri

| Alan | Tip | Açıklama |
|------|-----|----------|
| `sth_RECno` | int | **Primary key** |
| `sth_tarih` | datetime | İşlem tarihi |
| `sth_evrakno_seri` | nvarchar(6) | Evrak seri |
| `sth_evrakno_sira` | int | Evrak sıra |
| `sth_stok_kod` | nvarchar(25) | Stok kodu (STOKLAR FK) |
| `sth_cari_kodu` | nvarchar(25) | Cari kod (CARI_HESAPLAR FK) |
| `sth_tip` | tinyint | Tip (giriş/çıkış) |
| `sth_cins` | tinyint | Cins |
| `sth_miktar` | float | Miktar |
| `sth_miktar2` | float | 2. miktar |
| `sth_birim_pntr` | tinyint | Birim pointer |
| `sth_tutar` | float | Tutar |
| `sth_vergi` | float | Vergi |
| `sth_maliyet_ana` | float | Maliyet |
| `sth_depo_no` | int | Depo no |
| `sth_lastup_date` | datetime | **Son güncelleme** |

**Satır sayısı:** 81,198
**Son değişiklik:** 2026-06-25 15:10:23

---

### 6. KASALAR — Kasa Tanımları

| Alan | Tip | Açıklama |
|------|-----|----------|
| `kas_RECno` | int | **Primary key** |
| `kas_kod` | nvarchar(25) | Kasa kodu |
| `kas_isim` | nvarchar(40) | Kasa ismi |
| `kas_tip` | tinyint | Tip |
| `kas_doviz_cinsi` | tinyint | Para birimi |
| `kas_lastup_date` | datetime | **Son güncelleme** |

**Satır sayısı:** 6

---

### 7. BARKOD_TANIMLARI — Barkod Tanımları

| Alan | Tip | Açıklama |
|------|-----|----------|
| `bar_RECno` | int | **Primary key** |
| `bar_stokkodu` | nvarchar(25) | Stok kodu (STOKLAR FK) |
| `bar_barkodtipi` | tinyint | Barkod tipi |
| `bar_icerigi` | tinyint | İçerik |
| `bar_birimpntr` | tinyint | Birim pointer |
| `bar_partikodu` | nvarchar(25) | Parti kodu |
| `bar_lotno` | int | Lot no |
| `bar_lastup_date` | datetime | **Son güncelleme** |

**Satır sayısı:** 4,461
**Son değişiklik:** 2026-06-25 12:21:35

---

## Ek Tablolar (sync için değerli)

| Tablo | Satır | Açıklama |
|-------|-------|----------|
| `STOK_SATIS_FIYAT_LISTELERI` | 12,778 | Satış fiyat listeleri |
| `DEPOLAR` | 1 | Depo tanımları |
| `CARI_HESAP_GRUPLARI` | 0 | Cari grup tanımları |
| `MIKRO_SYNC_DELETED_LOG` | ? | Silinen kayıt logu (Mikro'nun kendi sync aracı) |
| `SYNC_LOGS` | ? | Sync logları |
| `SYNC_QUEUE` | ? | Sync kuyruğu |

---

## Sync Stratejisi (Onaylanmış)

```
Window Agent (C#)
  ↓
  MikroClient.ReadTableAsync(table, checkpoint)
    SQL: SELECT * FROM dbo.{TABLE}
         WHERE {lastup_date} > @checkpoint
         ORDER BY {lastup_date}
         OFFSET 0 ROWS FETCH NEXT 5001 ROWS ONLY
    ↓
  checkpoint = en son {lastup_date} değeri
    ↓
  RemoteSyncClient.PushBatchAsync(rows)
    ↓
  FieldOps API → PostgreSQL (RLS ile tenant isolation)
```

**Batch boyutu:** 5,000 satır (paginated — her seferinde 5,001 çekilir, 5,001 varsa "hasMore")
**Retry:** 3 deneme (1s / 5s / 30s) → dead-letter

---

## Önemli Notlar

1. **ROWVERSION yok** — Mikro V15'te `*_lastup_date` ile delta sync yapılır.
2. **Tüm tablolarda `*_RECno` primary key olarak kullanılabilir** — integer, unique, hiçbir zaman değişmez.
3. **Müşteri kodları (`*_kod`) değişebilir** — `*_RECno` ile join edilir.
4. **SIPARISLER'de 1 satır var** — sipariş verileri başka bir sisteme giriliyor olabilir.
5. **Tahsilat = evrak_tip=63** (KASA tahsilat, `cha_miktari=0`, tutar `cha_meblag`'da).
6. **Havale/EFT = evrak_tip=64** — büyük tutarlar genelde bank transferi.
7. **MIKRO_SYNC_DELETED_LOG** tablosu — Mikro'nun kendi sync aracının sildiği kayıtları logluyor.
   Bu tablo, silinen kayıtları sunucudan da silmek için kullanılabilir (F5).
