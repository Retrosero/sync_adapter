package com.fieldops.examples

import com.fieldops.FieldOpsConfig
import com.fieldops.api.FieldOpsClient
import com.fieldops.api.FieldOpsException
import com.fieldops.dto.SyncDataRow
import com.fieldops.repository.FieldOpsRepository
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch
import kotlinx.serialization.json.JsonElement
import java.util.UUID

/**
 * FieldOps Android Entegrasyonu — Kullanım Örnekleri
 *
 * Bu dosya saha satış uygulamanızda FieldOps SDK'sının nasıl kullanılacağını gösterir.
 * Gerçek uygulamada bu kodları kendi ViewModel/Repository katmanlarınıza uyarlayın.
 */

// ══════════════════════════════════════════════════════════════════════════════
// 1. Kurulum — Application sınıfında veya DI container'da
// ══════════════════════════════════════════════════════════════════════════════

object FieldOpsSetup {

    // Gerçek uygulamada: SharedPreferences veya EncryptedSharedPreferences kullanın
    private fun getSavedPrefs(): Triple<String, String, String> {
        val prefs = android.preference.PreferenceManager.getDefaultSharedPreferences(
            // Android Context — uygulamadan alın
        )
        val tenantId = prefs.getString("tenant_id", "") ?: ""
        val apiKey = prefs.getString("api_key", "") ?: ""
        val deviceId = prefs.getString("device_id", "") ?: ""
        return Triple(tenantId, apiKey, deviceId)
    }

    // Global singleton örneği
    val repository: FieldOpsRepository by lazy {
        val (tenantId, apiKey, deviceId) = getSavedPrefs()
        val config = FieldOpsConfig(
            baseUrl = "https://api.fieldops.example.com", // Sunucu URL'iniz
            tenantId = tenantId,
            apiKey = apiKey,
            deviceId = deviceId,
        )
        val client = FieldOpsClient(config)
        FieldOpsRepository(client, config)
    }

    // Manuel kurulum (test için)
    fun createRepository(
        baseUrl: String,
        tenantId: String,
        apiKey: String,
        deviceId: String,
    ): FieldOpsRepository {
        val config = FieldOpsConfig(
            baseUrl = baseUrl,
            tenantId = tenantId,
            apiKey = apiKey,
            deviceId = deviceId,
        )
        return FieldOpsRepository(FieldOpsClient(config), config)
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// 2. Satış Siparişi — Outbox push
// ══════════════════════════════════════════════════════════════════════════════

/**
 * Satış siparişi oluşturulduğunda çağrılır.
 * Sırası:
 *   1. Belgeyi local SQLite outbox'a yaz (idempotency_key ile)
 *   2. İnternet varsa sunucuya push et
 *   3. Başarılı olursa local outbox'dan sil
 */
suspend fun submitSalesOrder(
    cariKod: String,
    siparisTarihi: String,        // "2026-06-25"
    gonderimTarihi: String,        // "2026-06-28"
    satirlar: List<SiparisSatiri>,
    repository: FieldOpsRepository,
    onError: (String) -> Unit,
) {
    // 1) Outbox payload oluştur
    val payload = buildMap<String, kotlinx.serialization.json.JsonElement> {
        put("cari_kod", kotlinx.serialization.json.JsonPrimitive(cariKod))
        put("siparis_tarihi", kotlinx.serialization.json.JsonPrimitive(siparisTarihi))
        put("gonderim_tarihi", kotlinx.serialization.json.JsonPrimitive(gonderimTarihi))
        put("durum", kotlinx.serialization.json.JsonPrimitive("yeni"))
        put("satir_count", kotlinx.serialization.json.JsonPrimitive(satirlar.size.toDouble()))
        put("satirlar", kotlinx.serialization.json.JsonArray(satirlar.map { satir ->
            kotlinx.serialization.json.JsonObject(buildMap {
                put("stok_kod", kotlinx.serialization.json.JsonPrimitive(satir.stokKod))
                put("miktar", kotlinx.serialization.json.JsonPrimitive(satir.miktar))
                put("birim_fiyat", kotlinx.serialization.json.JsonPrimitive(satir.birimFiyat))
                put("kdv_orani", kotlinx.serialization.json.JsonPrimitive(satir.kdvOrani))
            })
        }))
    }

    // 2) Push et
    try {
        val result = repository.pushDocument(
            documentType = "sales_order",
            payload = payload,
        )
        if (result.accepted.isNotEmpty()) {
            // Sunucu kabul etti — local outbox'dan temizle
            // localDb.deleteOutboxItems(result.accepted)
        }
        if (result.rejected.isNotEmpty()) {
            onError("Sunucu reddetti: ${result.rejected.first().second}")
        }
    } catch (e: FieldOpsException) {
        // Ağ hatası — item local outbox'ta kalsın, biraz sonra tekrar dene
        onError("Ağ hatası: ${e.message}")
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// 3. Tahsilat — Outbox push
// ══════════════════════════════════════════════════════════════════════════════

suspend fun submitCollection(
    cariKod: String,
    makbuzNo: String,
    tahsilatTarihi: String,
    tahsilatTutari: Double,
    odemeSekli: String,           // "nakit" | "cek" | "havale"
    aciklama: String?,
    repository: FieldOpsRepository,
    onError: (String) -> Unit,
) {
    val payload = buildMap<String, kotlinx.serialization.json.JsonElement> {
        put("cari_kod", kotlinx.serialization.json.JsonPrimitive(cariKod))
        put("makbuz_no", kotlinx.serialization.json.JsonPrimitive(makbuzNo))
        put("tarih", kotlinx.serialization.json.JsonPrimitive(tahsilatTarihi))
        put("tutar", kotlinx.serialization.json.JsonPrimitive(tahsilatTutari))
        put("odeme_sekli", kotlinx.serialization.json.JsonPrimitive(odemeSekli))
        if (aciklama != null) put("aciklama", kotlinx.serialization.json.JsonPrimitive(aciklama))
    }
    try {
        repository.pushDocument("collection", payload)
    } catch (e: FieldOpsException) {
        onError("Tahsilat gönderilemedi: ${e.message}")
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// 4. Delta Sync — Sunucudan veri çekme
// ══════════════════════════════════════════════════════════════════════════════

/**
 * Uygulama açıldığında veya periyodik olarak (WorkManager) çağrılır.
 * Sadece son sync'den sonra değişen kayıtları çeker.
 */
suspend fun syncAllTables(
    repository: FieldOpsRepository,
    onProgress: (table: String, rows: Int) -> Unit,
    onError: (String) -> Unit,
) {
    val tables = listOf(
        "cari_hesaplar",
        "stoklar",
        "barkod_tanimlari",
        "cari_hesap_hareketleri",
    )

    for (tableName in tables) {
        try {
            val count = repository.pullTable(tableName) { row ->
                // row.payload → typed modele map et ve SQLite'a yaz
                when (tableName) {
                    "cari_hesaplar" -> upsertCariHesap(row)
                    "stoklar" -> upsertStok(row)
                    "barkod_tanimlari" -> upsertBarkod(row)
                    "cari_hesap_hareketleri" -> upsertCariHareket(row)
                }
            }
            onProgress(tableName, count)
        } catch (e: FieldOpsException) {
            onError("$tableName sync hatası: ${e.message}")
            // Hata olsa bile diğer tablolarla devam et
        }
    }
}

/**
 * Sunucudan gelen satır → SQLite CariHesap tablosuna yaz veya güncelle.
 * Upsert (INSERT OR REPLACE) kullanılır — idempotent.
 */
private fun upsertCariHesap(row: SyncDataRow) {
    val payload = row.payload
    // payload["cari_kod"] → JsonElement olarak gelir
    // JsonElement'ten değer okuma örneği:
    // val cariKod = (payload["cari_kod"] as? JsonPrimitive)?.content
    // val cariAd = (payload["cari_ad"] as? JsonPrimitive)?.content
    //
    // localDb.execute("""
    //   INSERT OR REPLACE INTO cari_hesaplar (id, cari_kod, cari_ad, ...)
    //   VALUES (?, ?, ?, ...)
    // """, ...)
}

// Diğer entity'ler için aynı pattern...
private fun upsertStok(row: SyncDataRow) {}
private fun upsertBarkod(row: SyncDataRow) {}
private fun upsertCariHareket(row: SyncDataRow) {}

// ══════════════════════════════════════════════════════════════════════════════
// 5. Checkpoint Persistence — Uygulama kapanırken/başlarken
// ══════════════════════════════════════════════════════════════════════════════

/**
 * Uygulama kapanırken tüm checkpoint'leri SQLite'a kaydet.
 * Böylece bir sonraki açılışta kaldığı yerden devam eder.
 */
suspend fun saveCheckpoints(repository: FieldOpsRepository) {
    val checkpoints = repository.getAllCheckpoints()
    // localDb.transaction {
    //   checkpoints.forEach { (table, checkpoint) ->
    //     localDb.upsert("sync_checkpoints", table to checkpoint)
    //   }
    // }
}

/**
 * Uygulama başlangıcında checkpoint'leri yükle.
 */
fun loadCheckpoints(repository: FieldOpsRepository) {
    // val saved = localDb.query("SELECT * FROM sync_checkpoints")
    // saved.forEach { row ->
    //   repository.setCheckpoint(row.tableName, row.checkpoint)
    // }
}

// ══════════════════════════════════════════════════════════════════════════════
// Yardımcı tipler
// ══════════════════════════════════════════════════════════════════════════════

data class SiparisSatiri(
    val stokKod: String,
    val miktar: Double,
    val birimFiyat: Double,
    val kdvOrani: Double,
)
