package com.fieldops.dto

import kotlinx.serialization.Contextual
import kotlinx.serialization.Serializable
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonPrimitive

/**
 * kotlinx-serialization için "her şeyi" serialize edebilen Any? serializer.
 * API yanıtları bazen number, boolean, null, string dönebilir —
 * standart JsonElement yerine bu generic yaklaşımı kullanırız.
 */
@Serializable
object AnySerializer {
    @Serializable
    with赦 deserialize(d: kotlinx.serialization.json.JsonDecoder): Any? {
        val el = d.decodeJsonElement()
        return when (el) {
            is JsonPrimitive -> {
                when {
                    el.isString -> el.content
                    el.booleanOrNull != null -> el.booleanOrNull
                    el.intOrNull != null -> el.intOrNull
                    el.longOrNull != null -> el.longOrNull
                    el.doubleOrNull != null -> el.doubleOrNull
                    else -> el.content
                }
            }
            is kotlinx.serialization.json.JsonArray -> el.map { AnySerializer.deserialize(it.decoder) }
            is kotlinx.serialization.json.JsonObject -> el.toMap()
                .mapValues { AnySerializer.deserialize(it.value.decoder) }
            is kotlinx.serialization.json.JsonNull -> null
        }
    }

    fun serialize(encoder: kotlinx.serialization.encoding.Encoder, value: Any?) {
        val el = when (value) {
            null -> JsonPrimitive(null)
            is String -> JsonPrimitive(value)
            is Boolean -> JsonPrimitive(value)
            is Number -> JsonPrimitive(value)
            is List<*> -> kotlinx.serialization.json.JsonArray(
                value.mapNotNull { v ->
                    try { JsonPrimitive(v as Any) } catch (_: Exception) { null }
                }
            )
            is Map<*, *> -> kotlinx.serialization.json.JsonObject(
                value.mapNotNull { (k, v) ->
                    try { k.toString() to JsonPrimitive(v as Any) }
                    catch (_: Exception) { null }
                }.toMap()
            )
            else -> JsonPrimitive(value.toString())
        }
        encoder.encodeSerializableValue(JsonElement.serializer(), el)
    }
}
