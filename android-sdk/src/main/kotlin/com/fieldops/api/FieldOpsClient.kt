package com.fieldops.api

import com.fieldops.FieldOpsConfig
import com.fieldops.dto.*
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.Response
import java.io.IOException

/**
 * FieldOps API client — Android'den sunucuya HTTP istekleri atar.
 *
 * Kurulum:
 * ```
 * val config = FieldOpsConfig(
 *     baseUrl = "https://api.fieldops.example.com",
 *     tenantId = "uuid-from-admin",
 *     apiKey = "fo_live_...",
 *     deviceId = androidId,
 * )
 * val client = FieldOpsClient(config)
 * ```
 *
 * Tüm istekler coroutine içinde çalışır. Başarısız istekler [FieldOpsException] fırlatır.
 *
 * @throws FieldOpsException network/sunucu hatalarında
 * @throws FieldOpsAuthException 401/403 — API key geçersiz veya tenant pasif
 * @throws FieldOpsServerException 5xx — sunucu hatası (detay: [FieldOpsException.message])
 */
class FieldOpsClient(private val config: FieldOpsConfig) {

    private val json = Json {
        ignoreUnknownKeys = true
        encodeDefaults = true
        prettyPrint = false
    }

    private val client: OkHttpClient = config.okHttpClientFactory?.invoke()
        ?: OkHttpClient.Builder()
            .connectTimeout(30, java.util.concurrent.TimeUnit.SECONDS)
            .readTimeout(60, java.util.concurrent.TimeUnit.SECONDS)
            .writeTimeout(60, java.util.concurrent.TimeUnit.SECONDS)
            .addInterceptor { chain ->
                val req = chain.request().newBuilder()
                    .addHeader("X-Tenant-Id", config.tenantId)
                    .addHeader("X-Api-Key", config.apiKey)
                    .addHeader("Content-Type", "application/json")
                    .build()
                chain.proceed(req)
            }
            .build()

    private inline fun <reified T> Response.parse(): T {
        if (!isSuccessful) {
            val bodyStr = body?.string() ?: ""
            when (code) {
                401, 403 -> throw FieldOpsAuthException(code, bodyStr)
                else -> throw FieldOpsServerException(code, bodyStr)
            }
        }
        val bodyStr = body?.string() ?: "{}"
        return json.decodeFromString<T>(bodyStr)
    }

    // ─── Outbox (Android → Server) ───────────────────────────────────────────

    /**
     * Yeni sipariş/tahsilat vb. belgeleri sunucuya gönderir.
     * idempotency_key tek seferliktir — aynı key ile tekrar gönderilirse
     * HTTP 200 döner ama yeni insert yapılmaz (idempotent).
     *
     * @param items Gönderilecek belgeler
     * @return Sunucu yanıtı — hangi item'lar kabul edildi, hangileri reddedildi
     */
    suspend fun pushOutbox(items: List<OutboxItem>): OutboxPushResponse = withContext(Dispatchers.IO) {
        val req = OutboxPushRequest(
            tenantId = config.tenantId,
            deviceId = config.deviceId,
            items = items,
        )
        val body = json.encodeToString(req).toRequestBody("application/json".toMediaType())
        val request = Request.Builder()
            .url("${config.baseUrl}/api/v1/outbox/push")
            .post(body)
            .build()
        client.newCall(request).execute().use { parse() }
    }

    // ─── Data Pull (Server → Android) ────────────────────────────────────────

    /**
     * Belirli bir tablodaki verileri çeker.
     * Delta sync için [since] parametresi kullanılır — sadece son sync'den
     * sonra değişen kayıtlar gelir.
     *
     * @param tableName MikroDB tablo adı — "cari_hesaplar", "stoklar" vb.
     * @param since Son UTC timestamp (ISO-8601) — null verilirse tüm tablo çekilir
     * @param page 1-indexed sayfa numarası
     * @param pageSize Sayfa başı kayıt (max 1000)
     */
    suspend fun pullTable(
        tableName: String,
        since: String? = null,
        page: Int = 1,
        pageSize: Int = 200,
    ): DataPullResponse = withContext(Dispatchers.IO) {
        val url = buildString {
            append("${config.baseUrl}/api/v1/data/${tableName}")
            append("?page=$page&pageSize=$pageSize")
            if (since != null) append("&since=$since")
        }
        val request = Request.Builder()
            .url(url)
            .get()
            .build()
        client.newCall(request).execute().use { parse() }
    }

    /**
     * Sunucudaki tüm tabloların listesini ve son sync zamanlarını döner.
     * Android ilk sync öncesi bu endpoint'i çağırarak hangi tabloların
     * güncel olduğunu kontrol edebilir.
     */
    suspend fun listTables(): List<TableInfo> = withContext(Dispatchers.IO) {
        val request = Request.Builder()
            .url("${config.baseUrl}/api/v1/data/")
            .get()
            .build()
        client.newCall(request).execute().use { parse() }
    }

    // ─── Health ───────────────────────────────────────────────────────────────

    /**
     * Sunucu erişilebilirlik kontrolü. İnternet bağlantısı var mı,
     * sunucu ayakta mı kontrol eder.
     */
    suspend fun health(): Map<String, Any?> = withContext(Dispatchers.IO) {
        val request = Request.Builder()
            .url("${config.baseUrl}/health")
            .get()
            .build()
        client.newCall(request).execute().use { resp ->
            if (!resp.isSuccessful) throw FieldOpsServerException(resp.code, resp.body?.string() ?: "")
            json.decodeFromString<Map<String, Any?>>(resp.body?.string() ?: "{}")
        }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// Exceptions
// ══════════════════════════════════════════════════════════════════════════════

open class FieldOpsException(msg: String, val code: Int? = null) : IOException(msg)

class FieldOpsAuthException(
    val httpCode: Int,
    body: String,
) : FieldOpsException("Kimlik doğrulama hatası ($httpCode): $body", httpCode)

class FieldOpsServerException(
    val httpCode: Int,
    body: String,
) : FieldOpsException("Sunucu hatası ($httpCode): $body", httpCode)
