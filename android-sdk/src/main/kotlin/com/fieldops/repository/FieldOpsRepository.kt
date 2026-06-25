package com.fieldops.repository

import com.fieldops.FieldOpsConfig
import com.fieldops.api.FieldOpsClient
import com.fieldops.api.FieldOpsException
import com.fieldops.dto.DataPullResponse
import com.fieldops.dto.OutboxItem
import com.fieldops.dto.OutboxPushResponse
import com.fieldops.dto.SyncDataRow
import com.fieldops.dto.TableInfo
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json

/**
 * FieldOps veri repository'si.
 * Android SQLite + FieldOpsClient arasında köprü kurar.
 *
 * Temel sorumlulukları:
 * 1. Outbox item'ları lokal SQLite'a yaz → sunucuya push → başarılıysa sil
 * 2. Sunucudan tablo verisi çek → lokal SQLite'a yaz
 * 3. Her tablo için son sync timestamp'ini (checkpoint) takip et
 *
 * Lokal depolama: Android uygulaması SQLite veya Room kullanır.
 * Bu repository sadece dışarıya (FieldOpsClient) veri aktarır/çeker —
 * lokal DB'ye yazma işi uygulamaya aittir.
 */
class FieldOpsRepository(private val client: FieldOpsClient, private val config: FieldOpsConfig) {

    private val json = Json { encodeDefaults = true }

    /** Her tablo için son checkpoint (SyncedAt UTC timestamp) */
    private val checkpoints = mutableMapOf<String, String?>()

    /** Outbox push mutex — aynı anda birden fazla push olmaz */
    private val pushLock = Mutex()

    // ─── Outbox ──────────────────────────────────────────────────────────────

    /**
     * Yeni bir outbox item oluşturur ve hemen sunucuya push eder.
     * Başarılı olursa [onSuccess] callback'ini çağırır.
     *
     * Kullanım:
     * ```
     * import kotlinx.serialization.json.JsonPrimitive
     * repository.pushDocument(
     *     documentType = "sales_order",
     *     payload = buildMap {
     *         put("cari_kod", JsonPrimitive("C001"))
     *         put("siparis_tarihi", JsonPrimitive("2026-06-25"))
     *         put("miktar", JsonPrimitive(10.0))
     *     }
     * ) { serverId ->
     *     // Sunucuya yazıldı — local outbox'tan sil
     *     localDb.deleteOutboxItem(key)
     * }
     * ```
     *
     * @param documentType "sales_order" | "collection" | "payment" | ...
     * @param payload Belge alanları — kotlinx.serialization.json.JsonPrimitive ile sarılmalı
     * @param idempotencyKey Belirtilmezse UUID üretilir
     * @param onSuccess Sunucu ACK aldıktan sonra çağrılır — local DB temizliği yapılır
     */
    suspend fun pushDocument(
        documentType: String,
        payload: Map<String, kotlinx.serialization.json.JsonElement>,
        idempotencyKey: String = java.util.UUID.randomUUID().toString(),
        onSuccess: ((serverId: String) -> Unit)? = null,
    ): OutboxItemResult {
        val createdAt = java.time.Instant.now().toString()
        val item = OutboxItem(
            idempotencyKey = idempotencyKey,
            documentType = documentType,
            payload = payload,
            deviceId = config.deviceId,
            createdAt = createdAt,
        )
        return pushDocuments(listOf(item), onSuccess)
    }

    /**
     * Birden fazla outbox item'ı toplu push eder.
     * Tüm item'lar aynı anda gönderilir, idempotentlik korunur.
     */
    suspend fun pushDocuments(
        items: List<OutboxItem>,
        onSuccess: ((String) -> Unit)? = null,
    ): OutboxItemResult = pushLock.withLock {
        val response = client.pushOutbox(items)
        val acceptedKeys = response.accepted.map { it.idempotencyKey to it.serverId }
        acceptedKeys.forEach { (key, serverId) -> onSuccess?.invoke(serverId) }
        return OutboxItemResult(
            accepted = response.accepted.map { it.idempotencyKey },
            rejected = response.rejected.map { it.idempotencyKey to it.message },
        )
    }

    // ─── Data Pull ───────────────────────────────────────────────────────────

    /**
     * Belirli bir tabloyu sunucudan çeker ve callback'e döner.
     * Delta sync otomatik yapılır — [checkpoints] içindeki son timestamp'ten
     * itibaren sadece yeni/değişen kayıtlar gelir.
     *
     * @param tableName "cari_hesaplar" | "stoklar" | "cari_hesap_hareketleri" vb.
     * @param onRow Her gelen satır için çağrılır — Android burada SQLite'a yazar
     * @param batchSize Sunucu pageSize (max 1000, önerilen: 200)
     * @return Kaç satır çekildi
     */
    suspend fun pullTable(
        tableName: String,
        onRow: (SyncDataRow) -> Unit,
        batchSize: Int = 200,
    ): Int {
        var totalRows = 0
        var page = 1
        var hasMore = true

        while (hasMore) {
            val response = client.pullTable(
                tableName = tableName,
                since = checkpoints[tableName],
                page = page,
                pageSize = batchSize,
            )
            response.rows.forEach { row ->
                onRow(row)
                totalRows++
            }
            // Checkpoint güncelle — en son syncedAt
            response.rows.maxByOrNull { it.syncedAt }?.let { lastRow ->
                checkpoints[tableName] = lastRow.syncedAt
            }
            hasMore = response.rows.size == batchSize
            page++
        }
        return totalRows
    }

    /**
     * Tüm tabloları sırayla çeker. İlk çağrıda full pull,
     * sonraki çağrılarda sadece delta.
     */
    suspend fun pullAll(onRow: (SyncDataRow) -> Unit): Int {
        val tables = client.listTables()
        var total = 0
        for (table in tables) {
            total += pullTable(table.tableName, onRow)
        }
        return total
    }

    /**
     * Checkpoint'i manuel ayarlar. Uygulama başlangıcında
     * SQLite'dan yüklenen son sync zamanını set etmek için kullanılır.
     */
    fun setCheckpoint(tableName: String, checkpoint: String?) {
        checkpoints[tableName] = checkpoint
    }

    /**
     * Checkpoint'i döner. Uygulama kapanırken veya periyodik olarak
     * SQLite'a kaydedilir.
     */
    fun getCheckpoint(tableName: String): String? = checkpoints[tableName]

    /**
     * Tüm checkpoint'leri döner. Uygulama kapanırken toplu kayıt için.
     */
    fun getAllCheckpoints(): Map<String, String?> = checkpoints.toMap()
}

// ─── Result ───────────────────────────────────────────────────────────────────

data class OutboxItemResult(
    /** Başarıyla kabul edilen idempotency key'ler */
    val accepted: List<String>,
    /** Reddedilen key + hata mesajı */
    val rejected: List<Pair<String, String>>,
)
