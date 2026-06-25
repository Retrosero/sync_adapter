plugins {
    kotlin("jvm") version "1.9.22"
    kotlin("plugin.serialization") version "1.9.22"
    `java-library`
}

group = "com.fieldops"
version = "0.1.0"

java {
    sourceCompatibility = JavaVersion.VERSION_17
    targetCompatibility = JavaVersion.VERSION_17
}

kotlin {
    jvmToolchain(17)
}

dependencies {
    // HTTP
    implementation("com.squareup.okhttp3:okhttp:4.12.0")

    // JSON — kotlinx.serialization, aktif seçim
    implementation("org.jetbrains.kotlinx:kotlinx-serialization-json:1.6.3")

    // Async
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-core:1.8.0")
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.8.0")

    // Logging
    implementation("org.slf4j:slf4j-android:1.7.36")

    // Android standart (lokal Room/SQLite varsa eklenir)
    // implementation("androidx.sqlite:sqlite-ktx:2.4.0")
}

tasks.withType<org.jetbrains.kotlin.gradle.tasks.KotlinCompile> {
    kotlinOptions {
        jvmTarget = "17"
        freeCompilerArgs = listOf("-opt-in=kotlinx.serialization.ExperimentalSerializationApi")
    }
}
