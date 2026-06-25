package com.fieldops.dto

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

// ══════════════════════════════════════════════════════════════════════════════
// Outbox — Android → Server (yeni sipariş, tahsilat vb.)
// ══════════════════════════════════════════════════════════════════════════════

/**
 * Android'in sunucuya gönderdiği tek bir outbox item.
 * idempotency_key tek seferlik UUID'dir — aynı key ile tekrar gelirse
 * sunucu HTTP 200 döner ama yeni insert yapmaz (idempotent).
 */
@Serializable
data class OutboxItem(
    /** UUID — her belge için cihazda bir kez üretilir, asla tekrar kullanılmaz */
    val idempotencyKey: String,
    /** "sales_order" | "collection" | "payment" | ... */
    val documentType: String,
    /**
     * Serbest JSON — sipariş/tahsilat alanları.
     * Kotlin tarafında kotlinx-serialization JsonElement olarak deserialize edilir.
     * Kullanmadan önce typed modele map edilir.
     */
    val payload: Map<String, @Serializable(with = JsonElementSerializer) JsonElement>,
    /** Opsiyonel — cihaz ID */
    val deviceId: String? = null,
    /** UTC timestamp — "2026-06-25T10:30:00Z" */
    val createdAt: String,
)

/** Android → Server push isteği */
@Serializable
data class OutboxPushRequest(
    val tenantId: String,
    val deviceId: String? = null,
    val items: List<OutboxItem>,
)

/** Sunucu ACK yanıtı */
@Serializable
data class OutboxPushResponse(
    val accepted: List<OutboxItemAck>,
    val rejected: List<OutboxItemReject>,
)

@Serializable
data class OutboxItemAck(
    val idempotencyKey: String,
    val serverId: String,
    val status: String,
    val acceptedAt: String,
)

@Serializable
data class OutboxItemReject(
    val idempotencyKey: String,
    val code: String,
    val message: String,
)

// ══════════════════════════════════════════════════════════════════════════════
// Data Pull — Android → Server (ERP'den çekilen veri)
// ══════════════════════════════════════════════════════════════════════════════

/**
 * Android'in sunucudan tablo bazında veri çekmesi.
 * Windows ajanı ERP→Server sync'ini tamamladıktan sonra Android bu endpoint'i çağırır.
 *
 * @param tableName MikroDB tablo adı — "cari_hesaplar", "stoklar", "cari_hesap_hareketleri" vb.
 * @param since Opsiyonel UTC timestamp — sadece bu zamandan sonra sync edilmiş kayıtları çeker.
 *              İlk sync için null verilir (tüm tablo gelir).
 * @param page 1-indexed sayfa numarası
 * @param pageSize Sayfa başı kayıt (max 1000)
 */
@Serializable
data class DataPullRequest(
    val tableName: String,
    val since: String? = null,    // ISO-8601 UTC
    val page: Int = 1,
    val pageSize: Int = 200,
)

/** Sunucudan dönen sayfalı veri yanıtı */
@Serializable
data class DataPullResponse(
    val tableName: String,
    val total: Int,
    val page: Int,
    val pageSize: Int,
    val rows: List<SyncDataRow>,
)

/**
 * Sunucudaki generic sync_data tablosundan dönen tek bir satır.
 * Payload — JSONB içeriği — tabloya göre farklı yapıdadır.
 * Android uygulaması bu Map'i typed modele çevirir.
 */
@Serializable
data class SyncDataRow(
    val id: String,
    val tableName: String,
    val sourcePk: String,         // MikroDB PK değeri (string)
    /**
     * Tablo verisi — JSONB deserialize edilmiş hali.
     * CariHesap için: { "cari_kod": "C001", "cari_ad": "...", ... }
     * Stok için: { "stok_kod": "S001", "stok_ad": "...", ... }
     * Android bu Map'i typed modele mapper.
     */
    val payload: Map<String, @Serializable(with = JsonElementSerializer) JsonElement>,
    val sourceModifiedAt: String?, // MikroDB'deki son değişiklik zamanı
    val syncedAt: String,          // Sunucuya yazılma zamanı
)

/** Sunucudaki tabloların listesi — Android hangi tabloları sync edeceğini bilir */
@Serializable
data class TableInfo(
    val tableName: String,
    val count: Int,
    val lastSyncedAt: String?,
)

// ══════════════════════════════════════════════════════════════════════════════
// Shared
// ══════════════════════════════════════════════════════════════════════════════

/** Standart API hata yanıtı */
@Serializable
data class ApiError(
    val code: String,
    val message: String,
    val traceId: String? = null,
)

/** Sayfalı liste yanıtı (generic) */
@Serializable
data class PagedResponse<T>(
    val items: List<T>,
    val totalCount: Int,
    val page: Int,
    val pageSize: Int,
)

/**
 * kotlinx-serialization JsonElementSerializer —
 * JSON object/array/primitive değerlerini serialize/deserialize eder.
 * payload alanları için kullanılır.
 */
@kotlinx.serialization.ExperimentalSerializationApi
@Serializable(with = JsonElementSerializer::class)
sealed class JsonElement {
    @kotlinx.serialization.ExperimentalSerializationApi
    object JsonElementSerializer : kotlinx.serialization.KSerializer<JsonElement> {
        private val impl = kotlinx.serialization.json.JsonElement.serializer()
        override val descriptor = impl.descriptor
        override fun deserialize(decoder: kotlinx.serialization.encoding.Decoder): JsonElement {
            return JsonElementImpl(impl.deserialize(decoder))
        }
        override fun serialize(encoder: kotlinx.serialization.encoding.Encoder, value: JsonElement) {
            (value as JsonElementImpl).delegate.serialize(encoder, value.delegate)
        }
    }
}

@kotlinx.serialization.Serializable
@kotlinx.serialization.TransitiveSerialization
private data class JsonElementImpl(
    val delegate: kotlinx.serialization.json.JsonElement,
) : JsonElement()
