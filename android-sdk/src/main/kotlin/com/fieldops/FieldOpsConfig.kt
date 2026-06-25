// settings.gradle.kts içine ekleyin:
// dependencyResolutionManagement {
//     repositories {
//         maven { url = uri("file:///${projectDir}/../../android-sdk") }
//     }
// }

package com.fieldops

/**
 * FieldOps SDK ayarları.
 * Uygulama başlangıcında bir kez doldurulur.
 */
data class FieldOpsConfig(
    /** Sunucu base URL — ör: "https://api.fieldops.example.com" */
    val baseUrl: String,

    /** Tenant ID — Super Admin tarafından üretilir */
    val tenantId: String,

    /** Tenant API Key — Super Admin tarafından üretilir, cihazda güvenli saklanmalı */
    val apiKey: String,

    /** Cihaz benzersiz ID — SQLite'den veya fingerprint'ten okunabilir */
    val deviceId: String,

    /**
     * OkHttp interceptor chain'i (logging, retry, etc.).
     * Verilmezse varsayılan OkHttpClient kullanılır.
     */
    val okHttpClientFactory: (() -> okhttp3.OkHttpClient)? = null,
) {
    init {
        require(baseUrl.isNotBlank()) { "baseUrl boş olamaz" }
        require(tenantId.isNotBlank()) { "tenantId boş olamaz" }
        require(apiKey.isNotBlank()) { "apiKey boş olamaz" }
    }

    /** Key güvenli saklanmalı — SharedPreferences Encryption veya Android Keystore kullanın. */
    companion object {
        const val PREFS_NAME = "fieldops_prefs"
        const val KEY_TENANT_ID = "tenant_id"
        const val KEY_API_KEY = "api_key"
        const val KEY_DEVICE_ID = "device_id"
    }
}
