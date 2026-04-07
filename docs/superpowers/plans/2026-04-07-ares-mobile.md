# ARES Mobile Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Construir ARES Mobile, un asistente Android con Gemma 4 corriendo completamente on-device via LiteRT-LM, con chat en tiempo real, memoria persistente, y 5 herramientas móviles (clipboard, cámara, voz, ubicación, alarma).

**Architecture:** MVVM con Jetpack Compose en la capa UI, AgentLoop como motor del agente (itera inferencia → tool call → inferencia hasta respuesta final), GemmaClient como abstracción sobre LiteRT-LM con streaming de tokens via Flow de Kotlin.

**Tech Stack:** Kotlin 2.0, Jetpack Compose, LiteRT-LM (`com.google.mediapipe:tasks-genai`), Room, Hilt, Kotlin Coroutines + Flow, Navigation Compose, CameraX, Google Play Services Location.

---

## Mapa de archivos

```
ARES-mobile/
├── settings.gradle.kts
├── build.gradle.kts                          (root)
└── app/
    ├── build.gradle.kts
    ├── src/
    │   ├── main/
    │   │   ├── AndroidManifest.xml
    │   │   └── java/com/ares/mobile/
    │   │       ├── AresApplication.kt
    │   │       ├── MainActivity.kt
    │   │       ├── ai/
    │   │       │   ├── GemmaClient.kt        — wrapper LiteRT-LM con streaming Flow
    │   │       │   ├── ModelManager.kt       — descarga, verifica SHA-256, gestiona archivo
    │   │       │   └── ModelRouter.kt        — elige E2B o E4B según RAM
    │   │       ├── agent/
    │   │       │   ├── AgentLoop.kt          — ciclo inferencia → tool call → respuesta
    │   │       │   ├── ConversationHistory.kt — historial en RAM + persistencia Room
    │   │       │   ├── ITool.kt              — interfaz base de tools
    │   │       │   ├── ToolDefinition.kt     — JSON schema de una tool
    │   │       │   ├── ToolResult.kt         — resultado de ejecutar una tool
    │   │       │   └── ToolRegistry.kt       — registro y dispatch de tools
    │   │       ├── data/
    │   │       │   ├── AppDatabase.kt
    │   │       │   ├── MessageEntity.kt
    │   │       │   ├── MemoryEntity.kt
    │   │       │   ├── ScheduledTaskEntity.kt
    │   │       │   ├── ConversationDao.kt
    │   │       │   ├── MemoryDao.kt
    │   │       │   └── ScheduledTaskDao.kt
    │   │       ├── di/
    │   │       │   └── AppModule.kt
    │   │       ├── tools/
    │   │       │   ├── AlarmTool.kt
    │   │       │   ├── CameraTool.kt
    │   │       │   ├── ClipboardTool.kt
    │   │       │   ├── LocationTool.kt
    │   │       │   └── VoiceTool.kt
    │   │       ├── ui/
    │   │       │   ├── components/
    │   │       │   │   ├── MessageBubble.kt
    │   │       │   │   ├── QuickActionsBar.kt
    │   │       │   │   └── TypingIndicator.kt
    │   │       │   ├── navigation/
    │   │       │   │   └── AppNavigation.kt
    │   │       │   ├── screens/
    │   │       │   │   ├── ChatScreen.kt
    │   │       │   │   ├── FirstRunScreen.kt
    │   │       │   │   ├── MemoryScreen.kt
    │   │       │   │   ├── SettingsScreen.kt
    │   │       │   │   └── TasksScreen.kt
    │   │       │   └── theme/
    │   │       │       ├── Color.kt
    │   │       │       ├── Theme.kt
    │   │       │       └── Type.kt
    │   │       └── viewmodel/
    │   │           ├── ChatViewModel.kt
    │   │           ├── MemoryViewModel.kt
    │   │           └── SettingsViewModel.kt
    │   └── test/java/com/ares/mobile/
    │       ├── ModelRouterTest.kt
    │       ├── ToolRegistryTest.kt
    │       └── AgentLoopTest.kt
```

---

## Task 1: Scaffolding del proyecto Android

**Files:**
- Create: `ARES-mobile/settings.gradle.kts`
- Create: `ARES-mobile/build.gradle.kts`
- Create: `ARES-mobile/app/build.gradle.kts`
- Create: `ARES-mobile/app/src/main/AndroidManifest.xml`

> Nota: Crea el proyecto desde Android Studio ("Empty Activity" con Kotlin + Compose) para que el IDE genere el wrapper de Gradle. Luego reemplaza los archivos de configuración con los de abajo.

- [ ] **Step 1: Crear el proyecto Android base en Android Studio**

  New Project → Empty Activity → Language: Kotlin → Min SDK: API 26 → Package: `com.ares.mobile` → Save location: `ARES-mobile/`

- [ ] **Step 2: Reemplazar `settings.gradle.kts`**

```kotlin
// ARES-mobile/settings.gradle.kts
pluginManagement {
    repositories {
        google { content { includeGroupByRegex("com\\.android.*"); includeGroupByRegex("com\\.google.*"); includeGroupByRegex("androidx.*") } }
        mavenCentral()
        gradlePluginPortal()
    }
}
dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories {
        google()
        mavenCentral()
    }
}
rootProject.name = "AresMobile"
include(":app")
```

- [ ] **Step 3: Reemplazar `build.gradle.kts` (root)**

```kotlin
// ARES-mobile/build.gradle.kts
plugins {
    alias(libs.plugins.android.application) apply false
    alias(libs.plugins.kotlin.android) apply false
    alias(libs.plugins.kotlin.compose) apply false
    alias(libs.plugins.hilt) apply false
    alias(libs.plugins.kotlin.serialization) apply false
    alias(libs.plugins.ksp) apply false
}
```

- [ ] **Step 4: Reemplazar `app/build.gradle.kts`**

```kotlin
// ARES-mobile/app/build.gradle.kts
plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.android)
    alias(libs.plugins.kotlin.compose)
    alias(libs.plugins.hilt)
    alias(libs.plugins.kotlin.serialization)
    alias(libs.plugins.ksp)
}

android {
    namespace = "com.ares.mobile"
    compileSdk = 35

    defaultConfig {
        applicationId = "com.ares.mobile"
        minSdk = 26
        targetSdk = 35
        versionCode = 1
        versionName = "1.0.0"
    }

    buildTypes {
        release {
            isMinifyEnabled = true
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlinOptions { jvmTarget = "17" }
    buildFeatures { compose = true }

    packaging {
        resources { excludes += "/META-INF/{AL2.0,LGPL2.1}" }
    }
}

dependencies {
    val composeBom = platform("androidx.compose:compose-bom:2024.12.01")
    implementation(composeBom)

    // Compose UI
    implementation("androidx.compose.ui:ui")
    implementation("androidx.compose.ui:ui-graphics")
    implementation("androidx.compose.material3:material3")
    implementation("androidx.compose.material:material-icons-extended")
    implementation("androidx.activity:activity-compose:1.9.3")
    implementation("androidx.navigation:navigation-compose:2.8.4")

    // Architecture
    implementation("androidx.core:core-ktx:1.15.0")
    implementation("androidx.lifecycle:lifecycle-runtime-ktx:2.8.7")
    implementation("androidx.lifecycle:lifecycle-viewmodel-compose:2.8.7")

    // Hilt
    implementation("com.google.dagger:hilt-android:2.52")
    implementation("androidx.hilt:hilt-navigation-compose:1.2.0")
    ksp("com.google.dagger:hilt-compiler:2.52")

    // Room
    implementation("androidx.room:room-runtime:2.6.1")
    implementation("androidx.room:room-ktx:2.6.1")
    ksp("androidx.room:room-compiler:2.6.1")

    // AI on-device (LiteRT-LM via MediaPipe GenAI)
    implementation("com.google.mediapipe:tasks-genai:0.10.14")

    // CameraX
    implementation("androidx.camera:camera-camera2:1.4.0")
    implementation("androidx.camera:camera-lifecycle:1.4.0")
    implementation("androidx.camera:camera-view:1.4.0")

    // Location
    implementation("com.google.android.gms:play-services-location:21.3.0")

    // JSON
    implementation("org.jetbrains.kotlinx:kotlinx-serialization-json:1.7.3")

    // Coroutines
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.9.0")

    // DataStore (para preferencias)
    implementation("androidx.datastore:datastore-preferences:1.1.1")

    // Tests
    testImplementation("junit:junit:4.13.2")
    testImplementation("org.jetbrains.kotlinx:kotlinx-coroutines-test:1.9.0")
    testImplementation("io.mockk:mockk:1.13.12")
    androidTestImplementation("androidx.test.ext:junit:1.2.1")
    androidTestImplementation("androidx.compose.ui:ui-test-junit4")
}
```

- [ ] **Step 5: Actualizar `gradle/libs.versions.toml`** para añadir los plugins de Hilt, KSP y serialization si el version catalog no los tiene ya. Abre el archivo y añade:

```toml
[versions]
hilt = "2.52"
ksp = "2.0.21-1.0.27"
kotlinSerialization = "2.0.21"

[plugins]
hilt = { id = "com.google.dagger.hilt.android", version.ref = "hilt" }
ksp = { id = "com.google.devtools.ksp", version.ref = "ksp" }
kotlin-serialization = { id = "org.jetbrains.kotlin.plugin.serialization", version.ref = "kotlinSerialization" }
```

- [ ] **Step 6: Escribir `AndroidManifest.xml`**

```xml
<!-- app/src/main/AndroidManifest.xml -->
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android">

    <uses-permission android:name="android.permission.CAMERA" />
    <uses-permission android:name="android.permission.RECORD_AUDIO" />
    <uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" />
    <uses-permission android:name="android.permission.ACCESS_COARSE_LOCATION" />
    <uses-permission android:name="android.permission.SCHEDULE_EXACT_ALARM" />
    <uses-permission android:name="android.permission.USE_EXACT_ALARM" />
    <uses-permission android:name="android.permission.INTERNET" />
    <uses-permission android:name="android.permission.VIBRATE" />

    <uses-feature android:name="android.hardware.camera" android:required="false" />

    <application
        android:name=".AresApplication"
        android:allowBackup="false"
        android:icon="@mipmap/ic_launcher"
        android:label="ARES"
        android:theme="@style/Theme.AresMobile"
        android:largeHeap="true">

        <activity
            android:name=".MainActivity"
            android:exported="true"
            android:windowSoftInputMode="adjustResize">
            <intent-filter>
                <action android:name="android.intent.action.MAIN" />
                <category android:name="android.intent.category.LAUNCHER" />
            </intent-filter>
        </activity>

        <receiver
            android:name=".tools.AlarmReceiver"
            android:exported="true">
            <intent-filter>
                <action android:name="com.ares.mobile.ALARM_TRIGGER" />
            </intent-filter>
        </receiver>

    </application>
</manifest>
```

- [ ] **Step 7: Crear carpetas de paquetes**

```bash
cd ARES-mobile/app/src/main/java/com/ares/mobile
mkdir -p ai agent data di tools ui/components ui/navigation ui/screens ui/theme viewmodel
mkdir -p ../../test/java/com/ares/mobile
```

- [ ] **Step 8: Sync Gradle y verificar que compila sin errores**

  En Android Studio: File → Sync Project with Gradle Files. Debe terminar sin errores.

- [ ] **Step 9: Commit**

```bash
git add ARES-mobile/
git commit -m "feat(mobile): scaffold Android project with Compose + Hilt + Room + LiteRT-LM deps"
```

---

## Task 2: Sistema de tema (Color, Tipo, Tema)

**Files:**
- Create: `app/src/main/java/com/ares/mobile/ui/theme/Color.kt`
- Create: `app/src/main/java/com/ares/mobile/ui/theme/Type.kt`
- Create: `app/src/main/java/com/ares/mobile/ui/theme/Theme.kt`
- Create: `app/src/main/res/values/themes.xml`

- [ ] **Step 1: Crear `Color.kt`**

```kotlin
// ui/theme/Color.kt
package com.ares.mobile.ui.theme

import androidx.compose.ui.graphics.Color

// Primarios neón
val NeonRed = Color(0xFFFF2020)
val NeonRedDim = Color(0xFF8B0000)
val NeonRedGlow = Color(0x1AFF2020)        // 10% opacidad para glows
val NeonRedBorder = Color(0x33FF2020)      // 20% opacidad para bordes

// Fondos
val BackgroundDeep = Color(0xFF050505)
val SurfaceDark = Color(0xFF0D0D0D)
val SurfaceVariantDark = Color(0xFF110000) // burbujas IA
val SurfaceElevated = Color(0xFF141414)

// Texto
val TextPrimary = Color(0xFFCCCCCC)        // texto usuario
val TextSecondary = Color(0xFF666666)
val TextAres = Color(0xFFFF9090)           // texto IA
val TextMuted = Color(0xFF333333)

// Bordes
val BorderSubtle = Color(0xFF1F1F1F)
val BorderGlow = Color(0x1AFF2020)

// Estado
val StatusOnline = Color(0xFFFF3030)
val StatusOffline = Color(0xFF333333)
```

- [ ] **Step 2: Crear `Type.kt`**

```kotlin
// ui/theme/Type.kt
package com.ares.mobile.ui.theme

import androidx.compose.material3.Typography
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.sp

val AresTypography = Typography(
    bodyMedium = TextStyle(
        fontFamily = FontFamily.Default,
        fontWeight = FontWeight.Normal,
        fontSize = 13.sp,
        lineHeight = 18.sp
    ),
    bodySmall = TextStyle(
        fontFamily = FontFamily.Monospace,
        fontWeight = FontWeight.Normal,
        fontSize = 10.sp,
        lineHeight = 14.sp
    ),
    labelSmall = TextStyle(
        fontFamily = FontFamily.Monospace,
        fontWeight = FontWeight.Medium,
        fontSize = 9.sp,
        letterSpacing = 0.5.sp
    ),
    titleMedium = TextStyle(
        fontFamily = FontFamily.Default,
        fontWeight = FontWeight.Bold,
        fontSize = 18.sp,
        letterSpacing = 4.sp
    ),
    titleSmall = TextStyle(
        fontFamily = FontFamily.Default,
        fontWeight = FontWeight.SemiBold,
        fontSize = 14.sp
    )
)
```

- [ ] **Step 3: Crear `Theme.kt`**

```kotlin
// ui/theme/Theme.kt
package com.ares.mobile.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable

private val AresDarkColorScheme = darkColorScheme(
    primary = NeonRed,
    onPrimary = BackgroundDeep,
    primaryContainer = NeonRedDim,
    onPrimaryContainer = TextAres,
    secondary = NeonRedDim,
    onSecondary = TextPrimary,
    background = BackgroundDeep,
    onBackground = TextPrimary,
    surface = SurfaceDark,
    onSurface = TextPrimary,
    surfaceVariant = SurfaceVariantDark,
    onSurfaceVariant = TextAres,
    outline = BorderSubtle,
    outlineVariant = BorderGlow,
    error = NeonRed,
    onError = BackgroundDeep,
    scrim = Color(0x80000000)
)

@Composable
fun AresTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = AresDarkColorScheme,
        typography = AresTypography,
        content = content
    )
}
```

Añade también `import androidx.compose.ui.graphics.Color` al principio de `Theme.kt` para el `scrim`.

- [ ] **Step 4: Crear `res/values/themes.xml`** (necesario para la Activity antes de que Compose tome el control)

```xml
<!-- app/src/main/res/values/themes.xml -->
<resources>
    <style name="Theme.AresMobile" parent="android:Theme.Material.NoTitleBar">
        <item name="android:windowBackground">@android:color/black</item>
        <item name="android:statusBarColor">@android:color/transparent</item>
        <item name="android:navigationBarColor">@android:color/black</item>
    </style>
</resources>
```

- [ ] **Step 5: Commit**

```bash
git add app/src/main/java/com/ares/mobile/ui/theme/ app/src/main/res/values/themes.xml
git commit -m "feat(mobile): add ARES neon red dark theme system"
```

---

## Task 3: Capa de datos (Room)

**Files:**
- Create: `data/MessageEntity.kt`
- Create: `data/MemoryEntity.kt`
- Create: `data/ScheduledTaskEntity.kt`
- Create: `data/ConversationDao.kt`
- Create: `data/MemoryDao.kt`
- Create: `data/ScheduledTaskDao.kt`
- Create: `data/AppDatabase.kt`
- Test: `test/java/com/ares/mobile/data/DatabaseTest.kt`

- [ ] **Step 1: Escribir los tests de Room (fallarán hasta el Step 5)**

```kotlin
// test/java/com/ares/mobile/data/DatabaseTest.kt
package com.ares.mobile.data

import android.content.Context
import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import kotlinx.coroutines.test.runTest
import org.junit.After
import org.junit.Assert.*
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class DatabaseTest {
    private lateinit var db: AppDatabase
    private lateinit var memoryDao: MemoryDao
    private lateinit var conversationDao: ConversationDao

    @Before
    fun setup() {
        val context = ApplicationProvider.getApplicationContext<Context>()
        db = Room.inMemoryDatabaseBuilder(context, AppDatabase::class.java).build()
        memoryDao = db.memoryDao()
        conversationDao = db.conversationDao()
    }

    @After
    fun teardown() { db.close() }

    @Test
    fun insertAndRetrieveMemory() = runTest {
        val entity = MemoryEntity(key = "nombre", value = "Carlos", timestamp = System.currentTimeMillis())
        memoryDao.upsert(entity)
        val result = memoryDao.getAll()
        assertEquals(1, result.size)
        assertEquals("Carlos", result[0].value)
    }

    @Test
    fun insertAndRetrieveMessages() = runTest {
        val msg = MessageEntity(role = "user", content = "Hola", timestamp = System.currentTimeMillis(), sessionId = "s1")
        conversationDao.insert(msg)
        val result = conversationDao.getBySession("s1")
        assertEquals(1, result.size)
        assertEquals("Hola", result[0].content)
    }

    @Test
    fun deleteMemoryByKey() = runTest {
        memoryDao.upsert(MemoryEntity(key = "ciudad", value = "Madrid", timestamp = 0))
        memoryDao.deleteByKey("ciudad")
        assertTrue(memoryDao.getAll().isEmpty())
    }
}
```

- [ ] **Step 2: Crear `MessageEntity.kt`**

```kotlin
// data/MessageEntity.kt
package com.ares.mobile.data

import androidx.room.Entity
import androidx.room.PrimaryKey

@Entity(tableName = "messages")
data class MessageEntity(
    @PrimaryKey(autoGenerate = true) val id: Long = 0,
    val sessionId: String,
    val role: String,          // "user" | "assistant" | "tool"
    val content: String,
    val toolName: String? = null,
    val imageBase64: String? = null,
    val timestamp: Long
)
```

- [ ] **Step 3: Crear `MemoryEntity.kt`**

```kotlin
// data/MemoryEntity.kt
package com.ares.mobile.data

import androidx.room.Entity
import androidx.room.PrimaryKey

@Entity(tableName = "memories")
data class MemoryEntity(
    @PrimaryKey val key: String,
    val value: String,
    val timestamp: Long
)
```

- [ ] **Step 4: Crear `ScheduledTaskEntity.kt`**

```kotlin
// data/ScheduledTaskEntity.kt
package com.ares.mobile.data

import androidx.room.Entity
import androidx.room.PrimaryKey

@Entity(tableName = "scheduled_tasks")
data class ScheduledTaskEntity(
    @PrimaryKey(autoGenerate = true) val id: Long = 0,
    val description: String,
    val triggerAtMillis: Long,
    val alarmId: Int,
    val isDone: Boolean = false
)
```

- [ ] **Step 5: Crear los DAOs**

```kotlin
// data/ConversationDao.kt
package com.ares.mobile.data

import androidx.room.*

@Dao
interface ConversationDao {
    @Insert suspend fun insert(message: MessageEntity)
    @Query("SELECT * FROM messages WHERE sessionId = :sessionId ORDER BY timestamp ASC")
    suspend fun getBySession(sessionId: String): List<MessageEntity>
    @Query("DELETE FROM messages WHERE sessionId = :sessionId")
    suspend fun clearSession(sessionId: String)
    @Query("SELECT DISTINCT sessionId FROM messages ORDER BY MAX(timestamp) DESC")
    suspend fun getAllSessionIds(): List<String>
}
```

```kotlin
// data/MemoryDao.kt
package com.ares.mobile.data

import androidx.room.*

@Dao
interface MemoryDao {
    @Upsert suspend fun upsert(memory: MemoryEntity)
    @Query("SELECT * FROM memories ORDER BY timestamp DESC")
    suspend fun getAll(): List<MemoryEntity>
    @Query("SELECT * FROM memories WHERE key = :key LIMIT 1")
    suspend fun getByKey(key: String): MemoryEntity?
    @Query("DELETE FROM memories WHERE key = :key")
    suspend fun deleteByKey(key: String)
    @Query("DELETE FROM memories")
    suspend fun clearAll()
}
```

```kotlin
// data/ScheduledTaskDao.kt
package com.ares.mobile.data

import androidx.room.*

@Dao
interface ScheduledTaskDao {
    @Insert suspend fun insert(task: ScheduledTaskEntity): Long
    @Query("SELECT * FROM scheduled_tasks WHERE isDone = 0 ORDER BY triggerAtMillis ASC")
    suspend fun getPending(): List<ScheduledTaskEntity>
    @Query("UPDATE scheduled_tasks SET isDone = 1 WHERE id = :id")
    suspend fun markDone(id: Long)
    @Query("DELETE FROM scheduled_tasks WHERE id = :id")
    suspend fun delete(id: Long)
    @Query("SELECT * FROM scheduled_tasks ORDER BY triggerAtMillis DESC")
    suspend fun getAll(): List<ScheduledTaskEntity>
}
```

- [ ] **Step 6: Crear `AppDatabase.kt`**

```kotlin
// data/AppDatabase.kt
package com.ares.mobile.data

import androidx.room.Database
import androidx.room.RoomDatabase

@Database(
    entities = [MessageEntity::class, MemoryEntity::class, ScheduledTaskEntity::class],
    version = 1,
    exportSchema = false
)
abstract class AppDatabase : RoomDatabase() {
    abstract fun conversationDao(): ConversationDao
    abstract fun memoryDao(): MemoryDao
    abstract fun scheduledTaskDao(): ScheduledTaskDao
}
```

- [ ] **Step 7: Ejecutar tests de Room**

  Estos son tests instrumentados — requieren emulador o dispositivo. En Android Studio, click derecho en `DatabaseTest` → Run.  
  Expected: 3 tests PASS.

- [ ] **Step 8: Commit**

```bash
git add app/src/main/java/com/ares/mobile/data/ app/src/androidTest/
git commit -m "feat(mobile): add Room database with conversation, memory and task entities"
```

---

## Task 4: ModelRouter y ModelManager

**Files:**
- Create: `ai/ModelRouter.kt`
- Create: `ai/ModelManager.kt`
- Test: `test/java/com/ares/mobile/ModelRouterTest.kt`

- [ ] **Step 1: Escribir tests de ModelRouter**

```kotlin
// test/java/com/ares/mobile/ModelRouterTest.kt
package com.ares.mobile

import com.ares.mobile.ai.ModelRouter
import com.ares.mobile.ai.GemmaModel
import org.junit.Assert.assertEquals
import org.junit.Test

class ModelRouterTest {
    @Test
    fun `selects E2B when available RAM less than 4GB`() {
        val model = ModelRouter.selectModel(availableRamBytes = 3_000_000_000L, forceModel = null)
        assertEquals(GemmaModel.E2B, model)
    }

    @Test
    fun `selects E4B when available RAM 4GB or more`() {
        val model = ModelRouter.selectModel(availableRamBytes = 4_000_000_000L, forceModel = null)
        assertEquals(GemmaModel.E4B, model)
    }

    @Test
    fun `force model overrides RAM check`() {
        val model = ModelRouter.selectModel(availableRamBytes = 8_000_000_000L, forceModel = GemmaModel.E2B)
        assertEquals(GemmaModel.E2B, model)
    }
}
```

- [ ] **Step 2: Ejecutar test — verificar que falla**

  Expected: compilation error "Unresolved reference: ModelRouter"

- [ ] **Step 3: Crear `ModelRouter.kt`**

```kotlin
// ai/ModelRouter.kt
package com.ares.mobile.ai

enum class GemmaModel(
    val tag: String,
    val displayName: String,
    val downloadUrl: String,
    val sha256: String,
    val expectedBytes: Long
) {
    E2B(
        tag = "gemma4-e2b-it-int4.task",
        displayName = "Gemma 4 E2B",
        downloadUrl = "https://storage.googleapis.com/mediapipe-models/llm_inference/gemma4_e2b_it_int4/float16/1/gemma4_e2b_it_int4.task",
        sha256 = "PLACEHOLDER_SHA256_E2B",  // reemplazar con hash oficial cuando esté disponible
        expectedBytes = 1_500_000_000L
    ),
    E4B(
        tag = "gemma4-e4b-it-int4.task",
        displayName = "Gemma 4 E4B",
        downloadUrl = "https://storage.googleapis.com/mediapipe-models/llm_inference/gemma4_e4b_it_int4/float16/1/gemma4_e4b_it_int4.task",
        sha256 = "PLACEHOLDER_SHA256_E4B",
        expectedBytes = 3_000_000_000L
    )
}

object ModelRouter {
    fun selectModel(availableRamBytes: Long, forceModel: GemmaModel?): GemmaModel {
        if (forceModel != null) return forceModel
        return if (availableRamBytes >= 4_000_000_000L) GemmaModel.E4B else GemmaModel.E2B
    }
}
```

> **Nota:** Las URLs y hashes SHA-256 son placeholders. Antes de ejecutar en producción consulta la página oficial de Google AI Edge para los enlaces definitivos de Gemma 4.

- [ ] **Step 4: Ejecutar tests — verificar PASS**

  Expected: 3 tests PASS.

- [ ] **Step 5: Crear `ModelManager.kt`**

```kotlin
// ai/ModelManager.kt
package com.ares.mobile.ai

import android.content.Context
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.withContext
import java.io.File
import java.net.HttpURLConnection
import java.net.URL
import java.security.MessageDigest

sealed class DownloadState {
    object Idle : DownloadState()
    data class Downloading(val progress: Float, val downloadedBytes: Long, val totalBytes: Long) : DownloadState()
    data class Verifying(val progress: Float) : DownloadState()
    object Complete : DownloadState()
    data class Error(val message: String) : DownloadState()
}

class ModelManager(private val context: Context) {

    fun modelFile(model: GemmaModel): File =
        File(context.filesDir, model.tag)

    fun isModelDownloaded(model: GemmaModel): Boolean =
        modelFile(model).exists()

    fun downloadModel(model: GemmaModel): Flow<DownloadState> = flow {
        emit(DownloadState.Downloading(0f, 0L, model.expectedBytes))
        val target = modelFile(model)
        val tmp = File(context.filesDir, "${model.tag}.tmp")

        try {
            withContext(Dispatchers.IO) {
                val conn = URL(model.downloadUrl).openConnection() as HttpURLConnection
                conn.connectTimeout = 30_000
                conn.readTimeout = 60_000
                conn.connect()

                val total = conn.contentLengthLong.takeIf { it > 0 } ?: model.expectedBytes
                var downloaded = 0L
                val buffer = ByteArray(8192)

                conn.inputStream.use { input ->
                    tmp.outputStream().use { output ->
                        var read: Int
                        while (input.read(buffer).also { read = it } != -1) {
                            output.write(buffer, 0, read)
                            downloaded += read
                            emit(DownloadState.Downloading(downloaded.toFloat() / total, downloaded, total))
                        }
                    }
                }
            }

            // Verificar SHA-256
            emit(DownloadState.Verifying(0f))
            if (model.sha256 != "PLACEHOLDER_SHA256_E2B" && model.sha256 != "PLACEHOLDER_SHA256_E4B") {
                val actualHash = withContext(Dispatchers.IO) { sha256(tmp) }
                if (actualHash != model.sha256) {
                    tmp.delete()
                    emit(DownloadState.Error("Hash SHA-256 no coincide. Descarga corrupta."))
                    return@flow
                }
            }

            withContext(Dispatchers.IO) {
                tmp.renameTo(target)
            }
            emit(DownloadState.Complete)

        } catch (e: Exception) {
            tmp.delete()
            emit(DownloadState.Error(e.message ?: "Error desconocido"))
        }
    }

    fun deleteModel(model: GemmaModel) {
        modelFile(model).delete()
    }

    fun availableRamBytes(): Long {
        val mi = android.app.ActivityManager.MemoryInfo()
        (context.getSystemService(Context.ACTIVITY_SERVICE) as android.app.ActivityManager)
            .getMemoryInfo(mi)
        return mi.availMem
    }

    private fun sha256(file: File): String {
        val digest = MessageDigest.getInstance("SHA-256")
        file.inputStream().use { input ->
            val buffer = ByteArray(8192)
            var read: Int
            while (input.read(buffer).also { read = it } != -1) {
                digest.update(buffer, 0, read)
            }
        }
        return digest.digest().joinToString("") { "%02x".format(it) }
    }
}
```

- [ ] **Step 6: Commit**

```bash
git add app/src/main/java/com/ares/mobile/ai/ app/src/test/
git commit -m "feat(mobile): add ModelRouter and ModelManager with SHA-256 verification"
```

---

## Task 5: GemmaClient (wrapper LiteRT-LM)

**Files:**
- Create: `ai/GemmaClient.kt`

- [ ] **Step 1: Crear `GemmaClient.kt`**

```kotlin
// ai/GemmaClient.kt
package com.ares.mobile.ai

import android.content.Context
import com.google.mediapipe.tasks.genai.llminference.LlmInference
import com.google.mediapipe.tasks.genai.llminference.LlmInference.LlmInferenceOptions
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.callbackFlow
import java.io.File

data class ChatMessage(
    val role: String,   // "user" | "model" | "tool"
    val content: String,
    val imageBase64: String? = null
)

sealed class StreamToken {
    data class Text(val value: String) : StreamToken()
    object Done : StreamToken()
    data class Error(val message: String) : StreamToken()
}

class GemmaClient(
    private val context: Context,
    private val modelFile: File,
    private val maxTokens: Int = 1024,
    private val numCtx: Int = 4096
) {
    private var inference: LlmInference? = null

    fun isLoaded(): Boolean = inference != null

    fun load() {
        if (inference != null) return
        val options = LlmInferenceOptions.builder()
            .setModelPath(modelFile.absolutePath)
            .setMaxTokens(maxTokens)
            .setTopK(40)
            .setTemperature(0.7f)
            .setRandomSeed(42)
            .build()
        inference = LlmInference.createFromOptions(context, options)
    }

    fun unload() {
        inference?.close()
        inference = null
    }

    /**
     * Convierte la lista de mensajes en el prompt de Gemma 4 (formato chat-template).
     * Gemma 4 usa el formato:
     *   <start_of_turn>user\n{mensaje}<end_of_turn>\n<start_of_turn>model\n
     */
    private fun buildPrompt(
        systemPrompt: String,
        messages: List<ChatMessage>
    ): String = buildString {
        append("<start_of_turn>user\n")
        append("$systemPrompt\n\n")
        for (msg in messages) {
            when (msg.role) {
                "user" -> {
                    append("<end_of_turn>\n<start_of_turn>user\n${msg.content}")
                }
                "model", "assistant" -> {
                    append("<end_of_turn>\n<start_of_turn>model\n${msg.content}")
                }
                "tool" -> {
                    append("<end_of_turn>\n<start_of_turn>user\n[Tool result]: ${msg.content}")
                }
            }
        }
        append("<end_of_turn>\n<start_of_turn>model\n")
    }

    /**
     * Genera una respuesta con streaming de tokens.
     * Emite StreamToken.Text para cada token parcial y StreamToken.Done al finalizar.
     */
    fun generateStream(
        systemPrompt: String,
        messages: List<ChatMessage>
    ): Flow<StreamToken> = callbackFlow {
        val llm = inference ?: run {
            trySend(StreamToken.Error("Modelo no cargado"))
            close()
            return@callbackFlow
        }

        val prompt = buildPrompt(systemPrompt, messages)

        try {
            llm.generateResponseAsync(
                prompt,
                { partialResult, done ->
                    if (partialResult != null && partialResult.isNotEmpty()) {
                        trySend(StreamToken.Text(partialResult))
                    }
                    if (done) {
                        trySend(StreamToken.Done)
                        close()
                    }
                },
                { error ->
                    trySend(StreamToken.Error(error.message ?: "Error de inferencia"))
                    close(error)
                }
            )
        } catch (e: Exception) {
            trySend(StreamToken.Error(e.message ?: "Error inesperado"))
            close(e)
        }

        awaitClose { /* LlmInference no requiere cancelación explícita por stream */ }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add app/src/main/java/com/ares/mobile/ai/GemmaClient.kt
git commit -m "feat(mobile): add GemmaClient LiteRT-LM wrapper with streaming Flow"
```

---

## Task 6: Sistema de herramientas (Tool foundation)

**Files:**
- Create: `agent/ITool.kt`
- Create: `agent/ToolDefinition.kt`
- Create: `agent/ToolResult.kt`
- Create: `agent/ToolRegistry.kt`
- Test: `test/java/com/ares/mobile/ToolRegistryTest.kt`

- [ ] **Step 1: Escribir test de ToolRegistry**

```kotlin
// test/java/com/ares/mobile/ToolRegistryTest.kt
package com.ares.mobile

import com.ares.mobile.agent.*
import kotlinx.coroutines.test.runTest
import org.junit.Assert.*
import org.junit.Test

class ToolRegistryTest {
    private val fakeTool = object : ITool {
        override val definition = ToolDefinition(
            name = "test_tool",
            description = "Una tool de prueba",
            parameters = mapOf("input" to mapOf("type" to "string", "description" to "El input"))
        )
        override suspend fun execute(args: Map<String, Any>): ToolResult =
            ToolResult.Success("echo: ${args["input"]}")
    }

    @Test
    fun `registered tool is callable by name`() = runTest {
        val registry = ToolRegistry()
        registry.register(fakeTool)
        val result = registry.execute("test_tool", mapOf("input" to "hola"))
        assertTrue(result is ToolResult.Success)
        assertEquals("echo: hola", (result as ToolResult.Success).output)
    }

    @Test
    fun `unknown tool returns Error`() = runTest {
        val registry = ToolRegistry()
        val result = registry.execute("no_existe", emptyMap())
        assertTrue(result is ToolResult.Error)
    }

    @Test
    fun `definitions returns all registered tools`() {
        val registry = ToolRegistry()
        registry.register(fakeTool)
        assertEquals(1, registry.definitions().size)
        assertEquals("test_tool", registry.definitions()[0].name)
    }
}
```

- [ ] **Step 2: Ejecutar test — verificar que falla**

  Expected: compilation error "Unresolved reference: ITool"

- [ ] **Step 3: Crear los tipos del sistema de tools**

```kotlin
// agent/ToolDefinition.kt
package com.ares.mobile.agent

import kotlinx.serialization.Serializable

@Serializable
data class ToolDefinition(
    val name: String,
    val description: String,
    val parameters: Map<String, Map<String, String>>  // nombre → {type, description}
) {
    /** Serializa la definición como JSON schema para inyectar en el system prompt. */
    fun toSchemaString(): String = buildString {
        appendLine("Tool: $name")
        appendLine("Description: $description")
        appendLine("Parameters:")
        parameters.forEach { (paramName, meta) ->
            appendLine("  - $paramName (${meta["type"]}): ${meta["description"]}")
        }
    }
}
```

```kotlin
// agent/ToolResult.kt
package com.ares.mobile.agent

sealed class ToolResult {
    data class Success(val output: String) : ToolResult()
    data class Error(val message: String) : ToolResult()
}
```

```kotlin
// agent/ITool.kt
package com.ares.mobile.agent

interface ITool {
    val definition: ToolDefinition
    suspend fun execute(args: Map<String, Any>): ToolResult
}
```

```kotlin
// agent/ToolRegistry.kt
package com.ares.mobile.agent

class ToolRegistry {
    private val tools = mutableMapOf<String, ITool>()

    fun register(tool: ITool) {
        tools[tool.definition.name] = tool
    }

    fun definitions(): List<ToolDefinition> = tools.values.map { it.definition }

    suspend fun execute(name: String, args: Map<String, Any>): ToolResult {
        val tool = tools[name] ?: return ToolResult.Error("Tool '$name' no encontrada")
        return tool.execute(args)
    }
}
```

- [ ] **Step 4: Ejecutar tests — verificar PASS**

  Expected: 3 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add app/src/main/java/com/ares/mobile/agent/ app/src/test/java/com/ares/mobile/ToolRegistryTest.kt
git commit -m "feat(mobile): add tool system foundation (ITool, ToolRegistry, ToolDefinition)"
```

---

## Task 7: Herramientas móviles

**Files:**
- Create: `tools/ClipboardTool.kt`
- Create: `tools/AlarmTool.kt`
- Create: `tools/AlarmReceiver.kt`
- Create: `tools/LocationTool.kt`
- Create: `tools/VoiceTool.kt`
- Create: `tools/CameraTool.kt`

- [ ] **Step 1: Crear `ClipboardTool.kt`**

```kotlin
// tools/ClipboardTool.kt
package com.ares.mobile.tools

import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import com.ares.mobile.agent.ITool
import com.ares.mobile.agent.ToolDefinition
import com.ares.mobile.agent.ToolResult

class ClipboardTool(private val context: Context) : ITool {
    override val definition = ToolDefinition(
        name = "clipboard",
        description = "Lee o escribe el contenido del portapapeles del dispositivo.",
        parameters = mapOf(
            "action" to mapOf("type" to "string", "description" to "'read' para leer, 'write' para escribir"),
            "text" to mapOf("type" to "string", "description" to "Texto a escribir (solo para action=write)")
        )
    )

    override suspend fun execute(args: Map<String, Any>): ToolResult {
        val clipboard = context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
        return when (args["action"] as? String) {
            "read" -> {
                val text = clipboard.primaryClip?.getItemAt(0)?.text?.toString()
                    ?: return ToolResult.Error("El portapapeles está vacío")
                ToolResult.Success(text)
            }
            "write" -> {
                val text = args["text"] as? String ?: return ToolResult.Error("Falta el parámetro 'text'")
                clipboard.setPrimaryClip(ClipData.newPlainText("ARES", text))
                ToolResult.Success("Texto copiado al portapapeles.")
            }
            else -> ToolResult.Error("Acción inválida. Usa 'read' o 'write'.")
        }
    }
}
```

- [ ] **Step 2: Crear `AlarmReceiver.kt` y `AlarmTool.kt`**

```kotlin
// tools/AlarmReceiver.kt
package com.ares.mobile.tools

import android.app.NotificationChannel
import android.app.NotificationManager
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.os.Build
import androidx.core.app.NotificationCompat

class AlarmReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        val description = intent.getStringExtra("description") ?: "Recordatorio ARES"
        val channelId = "ares_alarms"
        val nm = context.getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            nm.createNotificationChannel(
                NotificationChannel(channelId, "ARES Alarmas", NotificationManager.IMPORTANCE_HIGH)
            )
        }

        val notification = NotificationCompat.Builder(context, channelId)
            .setSmallIcon(android.R.drawable.ic_lock_idle_alarm)
            .setContentTitle("⬡ ARES")
            .setContentText(description)
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .setAutoCancel(true)
            .build()

        nm.notify(intent.getIntExtra("alarmId", 0), notification)
    }
}
```

```kotlin
// tools/AlarmTool.kt
package com.ares.mobile.tools

import android.app.AlarmManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import com.ares.mobile.agent.ITool
import com.ares.mobile.agent.ToolDefinition
import com.ares.mobile.agent.ToolResult
import java.text.SimpleDateFormat
import java.util.*

class AlarmTool(private val context: Context) : ITool {
    override val definition = ToolDefinition(
        name = "alarm",
        description = "Crea una alarma o recordatorio. Acepta hora en formato HH:mm (para hoy o mañana) o timestamp Unix en milisegundos.",
        parameters = mapOf(
            "time" to mapOf("type" to "string", "description" to "Hora en formato HH:mm o timestamp Unix ms"),
            "description" to mapOf("type" to "string", "description" to "Descripción del recordatorio")
        )
    )

    override suspend fun execute(args: Map<String, Any>): ToolResult {
        val timeArg = args["time"] as? String ?: return ToolResult.Error("Falta el parámetro 'time'")
        val description = args["description"] as? String ?: "Recordatorio"

        val triggerMillis = parseTime(timeArg) ?: return ToolResult.Error("Formato de hora inválido. Usa HH:mm o timestamp Unix ms.")

        val alarmId = (System.currentTimeMillis() % Int.MAX_VALUE).toInt()
        val intent = Intent(context, AlarmReceiver::class.java).apply {
            action = "com.ares.mobile.ALARM_TRIGGER"
            putExtra("alarmId", alarmId)
            putExtra("description", description)
        }
        val pendingIntent = PendingIntent.getBroadcast(
            context, alarmId, intent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val alarmManager = context.getSystemService(Context.ALARM_SERVICE) as AlarmManager
        alarmManager.setExactAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, triggerMillis, pendingIntent)

        val formatted = SimpleDateFormat("dd/MM HH:mm", Locale.getDefault()).format(Date(triggerMillis))
        return ToolResult.Success("Alarma creada para el $formatted: $description")
    }

    private fun parseTime(input: String): Long? {
        // Intenta como timestamp Unix ms
        input.toLongOrNull()?.let { if (it > 1_000_000_000_000L) return it }

        // Intenta como HH:mm
        val parts = input.split(":").mapNotNull { it.trim().toIntOrNull() }
        if (parts.size != 2) return null
        val (hours, minutes) = parts

        val cal = Calendar.getInstance().apply {
            set(Calendar.HOUR_OF_DAY, hours)
            set(Calendar.MINUTE, minutes)
            set(Calendar.SECOND, 0)
            set(Calendar.MILLISECOND, 0)
            if (timeInMillis <= System.currentTimeMillis()) add(Calendar.DAY_OF_MONTH, 1)
        }
        return cal.timeInMillis
    }
}
```

- [ ] **Step 3: Crear `LocationTool.kt`**

```kotlin
// tools/LocationTool.kt
package com.ares.mobile.tools

import android.Manifest
import android.content.Context
import android.content.pm.PackageManager
import androidx.core.content.ContextCompat
import com.ares.mobile.agent.ITool
import com.ares.mobile.agent.ToolDefinition
import com.ares.mobile.agent.ToolResult
import com.google.android.gms.location.LocationServices
import com.google.android.gms.location.Priority
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlin.coroutines.resume

class LocationTool(private val context: Context) : ITool {
    override val definition = ToolDefinition(
        name = "location",
        description = "Obtiene la ubicación GPS actual del dispositivo.",
        parameters = emptyMap()
    )

    override suspend fun execute(args: Map<String, Any>): ToolResult {
        if (ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_FINE_LOCATION)
            != PackageManager.PERMISSION_GRANTED) {
            return ToolResult.Error("Permiso de ubicación no concedido.")
        }

        return suspendCancellableCoroutine { cont ->
            val client = LocationServices.getFusedLocationProviderClient(context)
            client.getCurrentLocation(Priority.PRIORITY_BALANCED_POWER_ACCURACY, null)
                .addOnSuccessListener { location ->
                    if (location != null) {
                        cont.resume(ToolResult.Success("Latitud: ${location.latitude}, Longitud: ${location.longitude}, Precisión: ${location.accuracy}m"))
                    } else {
                        cont.resume(ToolResult.Error("No se pudo obtener la ubicación."))
                    }
                }
                .addOnFailureListener { e ->
                    cont.resume(ToolResult.Error(e.message ?: "Error de ubicación"))
                }
        }
    }
}
```

- [ ] **Step 4: Crear `VoiceTool.kt`**

```kotlin
// tools/VoiceTool.kt
package com.ares.mobile.tools

import android.content.Context
import android.speech.tts.TextToSpeech
import com.ares.mobile.agent.ITool
import com.ares.mobile.agent.ToolDefinition
import com.ares.mobile.agent.ToolResult
import kotlinx.coroutines.suspendCancellableCoroutine
import java.util.Locale
import kotlin.coroutines.resume

class VoiceTool(private val context: Context) : ITool {
    private var tts: TextToSpeech? = null

    override val definition = ToolDefinition(
        name = "tts",
        description = "Lee un texto en voz alta usando el sintetizador de voz del dispositivo.",
        parameters = mapOf(
            "text" to mapOf("type" to "string", "description" to "Texto a leer en voz alta")
        )
    )

    override suspend fun execute(args: Map<String, Any>): ToolResult {
        val text = args["text"] as? String ?: return ToolResult.Error("Falta el parámetro 'text'")
        return speak(text)
    }

    private suspend fun speak(text: String): ToolResult = suspendCancellableCoroutine { cont ->
        var ttsInstance: TextToSpeech? = null
        ttsInstance = TextToSpeech(context) { status ->
            if (status == TextToSpeech.SUCCESS) {
                ttsInstance?.language = Locale("es", "ES")
                ttsInstance?.speak(text, TextToSpeech.QUEUE_FLUSH, null, "ARES_TTS")
                tts = ttsInstance
                cont.resume(ToolResult.Success("Texto leído: $text"))
            } else {
                cont.resume(ToolResult.Error("No se pudo inicializar el sintetizador de voz"))
            }
        }
        cont.invokeOnCancellation { ttsInstance?.stop(); ttsInstance?.shutdown() }
    }

    fun shutdown() { tts?.stop(); tts?.shutdown(); tts = null }
}
```

- [ ] **Step 5: Crear `CameraTool.kt`**

```kotlin
// tools/CameraTool.kt
package com.ares.mobile.tools

import android.content.Context
import android.util.Base64
import com.ares.mobile.agent.ITool
import com.ares.mobile.agent.ToolDefinition
import com.ares.mobile.agent.ToolResult
import java.io.File

/**
 * CameraTool no captura directamente (requiere UI). Actúa como puente:
 * la UI expone un callback onPhotoReady que deposita el base64 aquí.
 * El AgentLoop detecta cuando el usuario invoca esta tool y le pide a
 * ChatViewModel que abra la cámara y llame a setLastPhoto() con el resultado.
 */
class CameraTool(private val context: Context) : ITool {
    private var pendingPhotoBase64: String? = null

    override val definition = ToolDefinition(
        name = "camera",
        description = "Captura una foto con la cámara del dispositivo y la pasa como imagen al modelo para análisis multimodal.",
        parameters = emptyMap()
    )

    override suspend fun execute(args: Map<String, Any>): ToolResult {
        val photo = pendingPhotoBase64
            ?: return ToolResult.Error("CAMERA_NEEDED") // señal especial: la UI debe abrir la cámara
        pendingPhotoBase64 = null
        return ToolResult.Success("[IMAGE:$photo]")
    }

    /** Llamado por ChatViewModel cuando la foto está disponible. */
    fun setLastPhoto(base64: String) { pendingPhotoBase64 = base64 }

    fun hasPhoto(): Boolean = pendingPhotoBase64 != null

    fun photoFromFile(file: File): String =
        Base64.encodeToString(file.readBytes(), Base64.NO_WRAP)
}
```

- [ ] **Step 6: Commit**

```bash
git add app/src/main/java/com/ares/mobile/tools/
git commit -m "feat(mobile): add 5 mobile tools (clipboard, alarm, location, voice, camera)"
```

---

## Task 8: ConversationHistory y AgentLoop

**Files:**
- Create: `agent/ConversationHistory.kt`
- Create: `agent/AgentLoop.kt`
- Test: `test/java/com/ares/mobile/AgentLoopTest.kt`

- [ ] **Step 1: Escribir tests del AgentLoop**

```kotlin
// test/java/com/ares/mobile/AgentLoopTest.kt
package com.ares.mobile

import com.ares.mobile.agent.*
import com.ares.mobile.ai.ChatMessage
import com.ares.mobile.ai.GemmaClient
import com.ares.mobile.ai.StreamToken
import io.mockk.*
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.runTest
import org.junit.Assert.*
import org.junit.Test

class AgentLoopTest {

    private fun makeRegistry(vararg tools: ITool): ToolRegistry {
        val registry = ToolRegistry()
        tools.forEach { registry.register(it) }
        return registry
    }

    @Test
    fun `direct response without tool call returns streamed text`() = runTest {
        val client = mockk<GemmaClient>()
        every { client.generateStream(any(), any()) } returns flowOf(
            StreamToken.Text("Hola"),
            StreamToken.Text(" mundo"),
            StreamToken.Done
        )

        val history = ConversationHistory()
        history.addUser("saluda")
        val loop = AgentLoop(client, makeRegistry(), history)

        val tokens = mutableListOf<String>()
        loop.run(onToken = { tokens.add(it) }, onToolCall = { _, _ -> })

        assertEquals(listOf("Hola", " mundo"), tokens)
    }

    @Test
    fun `tool call is parsed and executed`() = runTest {
        val client = mockk<GemmaClient>()
        // Primera llamada: responde con un tool call
        // Segunda llamada: responde con texto final
        every { client.generateStream(any(), any()) } returnsMany listOf(
            flowOf(StreamToken.Text("""{"tool":"test_tool","args":{"input":"ping"}}"""), StreamToken.Done),
            flowOf(StreamToken.Text("La tool respondió: pong"), StreamToken.Done)
        )

        val fakeTool = object : ITool {
            override val definition = ToolDefinition("test_tool", "test", mapOf("input" to mapOf("type" to "string", "description" to "x")))
            override suspend fun execute(args: Map<String, Any>) = ToolResult.Success("pong")
        }

        val history = ConversationHistory()
        history.addUser("ejecuta test_tool con ping")
        val loop = AgentLoop(client, makeRegistry(fakeTool), history)

        val toolCalls = mutableListOf<String>()
        loop.run(onToken = {}, onToolCall = { name, _ -> toolCalls.add(name) })

        assertEquals(listOf("test_tool"), toolCalls)
    }
}
```

- [ ] **Step 2: Ejecutar tests — verificar que fallan**

  Expected: compilation error "Unresolved reference: ConversationHistory"

- [ ] **Step 3: Crear `ConversationHistory.kt`**

```kotlin
// agent/ConversationHistory.kt
package com.ares.mobile.agent

import com.ares.mobile.ai.ChatMessage

class ConversationHistory(private val maxMessages: Int = 20) {
    private val messages = mutableListOf<ChatMessage>()

    fun addUser(content: String, imageBase64: String? = null) {
        messages.add(ChatMessage(role = "user", content = content, imageBase64 = imageBase64))
        trim()
    }

    fun addAssistant(content: String) {
        messages.add(ChatMessage(role = "model", content = content))
    }

    fun addToolResult(toolName: String, result: String) {
        messages.add(ChatMessage(role = "tool", content = "[$toolName]: $result"))
    }

    fun getAll(): List<ChatMessage> = messages.toList()

    fun clear() { messages.clear() }

    private fun trim() {
        while (messages.size > maxMessages) messages.removeAt(0)
    }
}
```

- [ ] **Step 4: Crear `AgentLoop.kt`**

```kotlin
// agent/AgentLoop.kt
package com.ares.mobile.agent

import com.ares.mobile.ai.GemmaClient
import com.ares.mobile.ai.StreamToken
import kotlinx.coroutines.flow.collect
import org.json.JSONObject

private const val MAX_TOOL_ITERATIONS = 5

private val SYSTEM_PROMPT_TEMPLATE = """
Eres ARES, un asistente de IA personal que corre completamente en el dispositivo del usuario, sin conexión a internet.
Eres conciso, directo y útil. Respondes siempre en el mismo idioma que el usuario.

Tienes acceso a las siguientes herramientas. Para usarlas, responde ÚNICAMENTE con JSON válido en este formato:
{"tool": "nombre_herramienta", "args": {"param": "valor"}}

Si no necesitas usar ninguna herramienta, responde directamente en texto normal.
NO mezcles JSON con texto — responde con uno u otro.

Herramientas disponibles:
%s
""".trimIndent()

class AgentLoop(
    private val client: GemmaClient,
    private val registry: ToolRegistry,
    private val history: ConversationHistory
) {
    suspend fun run(
        onToken: (String) -> Unit,
        onToolCall: (toolName: String, args: Map<String, Any>) -> Unit
    ) {
        val systemPrompt = SYSTEM_PROMPT_TEMPLATE.format(
            registry.definitions().joinToString("\n\n") { it.toSchemaString() }
        )

        var iterations = 0
        while (iterations < MAX_TOOL_ITERATIONS) {
            iterations++
            val responseBuffer = StringBuilder()

            client.generateStream(systemPrompt, history.getAll()).collect { token ->
                when (token) {
                    is StreamToken.Text -> {
                        responseBuffer.append(token.value)
                        // Solo emitimos tokens si no parece ser un tool call JSON
                        if (!responseBuffer.startsWith("{")) {
                            onToken(token.value)
                        }
                    }
                    is StreamToken.Done -> { /* handled below */ }
                    is StreamToken.Error -> {
                        onToken("[Error: ${token.message}]")
                        return
                    }
                }
            }

            val response = responseBuffer.toString().trim()

            // Intentar parsear como tool call
            val toolCall = parseToolCall(response)
            if (toolCall != null) {
                val (toolName, args) = toolCall
                onToolCall(toolName, args)
                history.addAssistant(response)

                val result = registry.execute(toolName, args)
                val resultText = when (result) {
                    is ToolResult.Success -> result.output
                    is ToolResult.Error -> "Error: ${result.message}"
                }
                history.addToolResult(toolName, resultText)
                // Continúa el loop para que el modelo genere la respuesta final
                continue
            }

            // No es tool call: respuesta final
            if (responseBuffer.startsWith("{")) {
                // Emitir el buffer completo que no emitimos antes
                onToken(response)
            }
            history.addAssistant(response)
            return
        }

        onToken("[Límite de iteraciones alcanzado]")
    }

    private fun parseToolCall(text: String): Pair<String, Map<String, Any>>? {
        if (!text.startsWith("{")) return null
        return try {
            val json = JSONObject(text)
            val toolName = json.optString("tool").takeIf { it.isNotBlank() } ?: return null
            val argsJson = json.optJSONObject("args") ?: return null
            val args = buildMap<String, Any> {
                argsJson.keys().forEach { key ->
                    put(key, argsJson.get(key))
                }
            }
            Pair(toolName, args)
        } catch (e: Exception) {
            null
        }
    }
}
```

- [ ] **Step 5: Ejecutar tests — verificar PASS**

  Expected: 2 tests PASS.

- [ ] **Step 6: Commit**

```bash
git add app/src/main/java/com/ares/mobile/agent/ app/src/test/java/com/ares/mobile/AgentLoopTest.kt
git commit -m "feat(mobile): add AgentLoop with tool call detection and ConversationHistory"
```

---

## Task 9: Hilt DI

**Files:**
- Create: `AresApplication.kt`
- Create: `di/AppModule.kt`

- [ ] **Step 1: Crear `AresApplication.kt`**

```kotlin
// AresApplication.kt
package com.ares.mobile

import android.app.Application
import dagger.hilt.android.HiltAndroidApp

@HiltAndroidApp
class AresApplication : Application()
```

- [ ] **Step 2: Crear `di/AppModule.kt`**

```kotlin
// di/AppModule.kt
package com.ares.mobile.di

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.preferencesDataStore
import androidx.room.Room
import com.ares.mobile.agent.ToolRegistry
import com.ares.mobile.ai.GemmaClient
import com.ares.mobile.ai.ModelManager
import com.ares.mobile.data.AppDatabase
import com.ares.mobile.tools.*
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

private val Context.dataStore: DataStore<Preferences> by preferencesDataStore(name = "ares_settings")

@Module
@InstallIn(SingletonComponent::class)
object AppModule {

    @Provides @Singleton
    fun provideDatabase(@ApplicationContext ctx: Context): AppDatabase =
        Room.databaseBuilder(ctx, AppDatabase::class.java, "ares.db").build()

    @Provides @Singleton
    fun provideModelManager(@ApplicationContext ctx: Context) = ModelManager(ctx)

    @Provides @Singleton
    fun provideGemmaClient(@ApplicationContext ctx: Context, modelManager: ModelManager): GemmaClient {
        // El modelo real se carga en FirstRunScreen; aquí solo creamos el cliente
        val model = com.ares.mobile.ai.ModelRouter.selectModel(
            modelManager.availableRamBytes(), null
        )
        return GemmaClient(ctx, modelManager.modelFile(model))
    }

    @Provides @Singleton
    fun provideToolRegistry(@ApplicationContext ctx: Context): ToolRegistry {
        return ToolRegistry().apply {
            register(ClipboardTool(ctx))
            register(AlarmTool(ctx))
            register(LocationTool(ctx))
            register(VoiceTool(ctx))
            register(CameraTool(ctx))
        }
    }

    @Provides @Singleton
    fun provideDataStore(@ApplicationContext ctx: Context): DataStore<Preferences> =
        ctx.dataStore
}
```

- [ ] **Step 3: Commit**

```bash
git add app/src/main/java/com/ares/mobile/AresApplication.kt app/src/main/java/com/ares/mobile/di/
git commit -m "feat(mobile): add Hilt DI modules (DB, GemmaClient, ToolRegistry, DataStore)"
```

---

## Task 10: ViewModels

**Files:**
- Create: `viewmodel/ChatViewModel.kt`
- Create: `viewmodel/MemoryViewModel.kt`
- Create: `viewmodel/SettingsViewModel.kt`

- [ ] **Step 1: Crear `ChatViewModel.kt`**

```kotlin
// viewmodel/ChatViewModel.kt
package com.ares.mobile.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.ares.mobile.agent.AgentLoop
import com.ares.mobile.agent.ConversationHistory
import com.ares.mobile.agent.ToolRegistry
import com.ares.mobile.ai.GemmaClient
import com.ares.mobile.tools.CameraTool
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.launch
import javax.inject.Inject

data class UiMessage(
    val id: Long = System.nanoTime(),
    val role: String,          // "user" | "assistant"
    val content: String,
    val toolBadge: String? = null,
    val isStreaming: Boolean = false
)

data class ChatUiState(
    val messages: List<UiMessage> = emptyList(),
    val isGenerating: Boolean = false,
    val modelName: String = "",
    val error: String? = null,
    val needsCameraCapture: Boolean = false
)

@HiltViewModel
class ChatViewModel @Inject constructor(
    private val gemmaClient: GemmaClient,
    private val toolRegistry: ToolRegistry
) : ViewModel() {

    private val _uiState = MutableStateFlow(ChatUiState())
    val uiState: StateFlow<ChatUiState> = _uiState.asStateFlow()

    private val history = ConversationHistory()

    fun sendMessage(text: String, imageBase64: String? = null) {
        if (_uiState.value.isGenerating) return

        history.addUser(text, imageBase64)
        val userMsg = UiMessage(role = "user", content = text)
        val placeholderId = System.nanoTime()
        val placeholder = UiMessage(id = placeholderId, role = "assistant", content = "", isStreaming = true)

        _uiState.update { it.copy(
            messages = it.messages + userMsg + placeholder,
            isGenerating = true,
            error = null
        )}

        viewModelScope.launch {
            val streamedContent = StringBuilder()
            var lastToolBadge: String? = null

            val loop = AgentLoop(gemmaClient, toolRegistry, history)
            loop.run(
                onToken = { token ->
                    streamedContent.append(token)
                    _uiState.update { state ->
                        val updated = state.messages.map { msg ->
                            if (msg.id == placeholderId) msg.copy(content = streamedContent.toString())
                            else msg
                        }
                        state.copy(messages = updated)
                    }
                },
                onToolCall = { toolName, _ ->
                    lastToolBadge = toolName
                    if (toolName == "camera") {
                        _uiState.update { it.copy(needsCameraCapture = true) }
                    }
                }
            )

            _uiState.update { state ->
                val finalized = state.messages.map { msg ->
                    if (msg.id == placeholderId) msg.copy(
                        content = streamedContent.toString(),
                        isStreaming = false,
                        toolBadge = lastToolBadge
                    )
                    else msg
                }
                state.copy(messages = finalized, isGenerating = false)
            }
        }
    }

    fun onPhotoCaptured(base64: String) {
        val cameraTool = toolRegistry.definitions().firstOrNull { it.name == "camera" } ?: return
        // Deposit photo in CameraTool and resume
        (toolRegistry as? ToolRegistry)?.let {
            // Access CameraTool to set the photo
        }
        _uiState.update { it.copy(needsCameraCapture = false) }
        sendMessage("[Foto capturada]", base64)
    }

    fun clearConversation() {
        history.clear()
        _uiState.update { it.copy(messages = emptyList()) }
    }
}
```

- [ ] **Step 2: Crear `MemoryViewModel.kt`**

```kotlin
// viewmodel/MemoryViewModel.kt
package com.ares.mobile.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.ares.mobile.data.MemoryDao
import com.ares.mobile.data.MemoryEntity
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

@HiltViewModel
class MemoryViewModel @Inject constructor(
    private val memoryDao: MemoryDao
) : ViewModel() {

    private val _memories = MutableStateFlow<List<MemoryEntity>>(emptyList())
    val memories: StateFlow<List<MemoryEntity>> = _memories.asStateFlow()

    init { load() }

    fun load() {
        viewModelScope.launch {
            _memories.update { memoryDao.getAll() }
        }
    }

    fun delete(key: String) {
        viewModelScope.launch {
            memoryDao.deleteByKey(key)
            load()
        }
    }

    fun clearAll() {
        viewModelScope.launch {
            memoryDao.clearAll()
            _memories.update { emptyList() }
        }
    }
}
```

- [ ] **Step 3: Crear `SettingsViewModel.kt`**

```kotlin
// viewmodel/SettingsViewModel.kt
package com.ares.mobile.viewmodel

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.*
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.ares.mobile.ai.GemmaModel
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.launch
import javax.inject.Inject

private val KEY_FORCE_MODEL = stringPreferencesKey("force_model")
private val KEY_MAX_TOKENS = intPreferencesKey("max_tokens")
private val KEY_TTS_ENABLED = booleanPreferencesKey("tts_enabled")
private val KEY_THINKING_MODE = booleanPreferencesKey("thinking_mode")

data class AppSettings(
    val forceModel: GemmaModel? = null,
    val maxTokens: Int = 1024,
    val ttsEnabled: Boolean = false,
    val thinkingMode: Boolean = false
)

@HiltViewModel
class SettingsViewModel @Inject constructor(
    private val dataStore: DataStore<androidx.datastore.preferences.core.Preferences>
) : ViewModel() {

    val settings: StateFlow<AppSettings> = dataStore.data.map { prefs ->
        AppSettings(
            forceModel = prefs[KEY_FORCE_MODEL]?.let { GemmaModel.valueOf(it) },
            maxTokens = prefs[KEY_MAX_TOKENS] ?: 1024,
            ttsEnabled = prefs[KEY_TTS_ENABLED] ?: false,
            thinkingMode = prefs[KEY_THINKING_MODE] ?: false
        )
    }.stateIn(viewModelScope, SharingStarted.Eagerly, AppSettings())

    fun setForceModel(model: GemmaModel?) {
        viewModelScope.launch {
            dataStore.edit { prefs ->
                if (model == null) prefs.remove(KEY_FORCE_MODEL)
                else prefs[KEY_FORCE_MODEL] = model.name
            }
        }
    }

    fun setMaxTokens(value: Int) {
        viewModelScope.launch { dataStore.edit { it[KEY_MAX_TOKENS] = value } }
    }

    fun setTtsEnabled(enabled: Boolean) {
        viewModelScope.launch { dataStore.edit { it[KEY_TTS_ENABLED] = enabled } }
    }

    fun setThinkingMode(enabled: Boolean) {
        viewModelScope.launch { dataStore.edit { it[KEY_THINKING_MODE] = enabled } }
    }
}
```

- [ ] **Step 4: Añadir provider de DAOs en `AppModule.kt`**

Añade al final del objeto `AppModule`:

```kotlin
@Provides fun provideConversationDao(db: AppDatabase) = db.conversationDao()
@Provides fun provideMemoryDao(db: AppDatabase) = db.memoryDao()
@Provides fun provideScheduledTaskDao(db: AppDatabase) = db.scheduledTaskDao()
```

- [ ] **Step 5: Commit**

```bash
git add app/src/main/java/com/ares/mobile/viewmodel/ app/src/main/java/com/ares/mobile/di/
git commit -m "feat(mobile): add ChatViewModel, MemoryViewModel, SettingsViewModel"
```

---

## Task 11: Componentes UI reutilizables

**Files:**
- Create: `ui/components/MessageBubble.kt`
- Create: `ui/components/TypingIndicator.kt`
- Create: `ui/components/QuickActionsBar.kt`

- [ ] **Step 1: Crear `MessageBubble.kt`**

```kotlin
// ui/components/MessageBubble.kt
package com.ares.mobile.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.graphics.BlurMaskFilter
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Paint
import androidx.compose.ui.graphics.drawscope.drawIntoCanvas
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.ares.mobile.ui.theme.*
import com.ares.mobile.viewmodel.UiMessage

@Composable
fun MessageBubble(message: UiMessage) {
    val isUser = message.role == "user"
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = if (isUser) Arrangement.End else Arrangement.Start
    ) {
        Column(
            modifier = Modifier.widthIn(max = 300.dp),
            horizontalAlignment = if (isUser) Alignment.End else Alignment.Start
        ) {
            if (!isUser) {
                Text(
                    text = "ARES",
                    color = NeonRed.copy(alpha = 0.5f),
                    fontSize = 9.sp,
                    letterSpacing = 1.sp,
                    modifier = Modifier.padding(start = 4.dp, bottom = 3.dp)
                )
            }

            Box(
                modifier = Modifier
                    .then(
                        if (!isUser) Modifier.glowEffect(NeonRed.copy(alpha = 0.06f), blurRadius = 12.dp)
                        else Modifier
                    )
                    .background(
                        color = if (isUser) SurfaceDark else SurfaceVariantDark,
                        shape = RoundedCornerShape(
                            topStart = 18.dp, topEnd = 18.dp,
                            bottomStart = if (isUser) 18.dp else 4.dp,
                            bottomEnd = if (isUser) 4.dp else 18.dp
                        )
                    )
                    .border(
                        width = 1.dp,
                        color = if (isUser) BorderSubtle else NeonRedBorder,
                        shape = RoundedCornerShape(
                            topStart = 18.dp, topEnd = 18.dp,
                            bottomStart = if (isUser) 18.dp else 4.dp,
                            bottomEnd = if (isUser) 4.dp else 18.dp
                        )
                    )
                    .padding(horizontal = 14.dp, vertical = 10.dp)
            ) {
                Text(
                    text = message.content,
                    color = if (isUser) TextPrimary else TextAres,
                    fontSize = 13.sp,
                    lineHeight = 18.sp
                )
            }

            message.toolBadge?.let { badge ->
                Spacer(modifier = Modifier.height(4.dp))
                Text(
                    text = "⚙ $badge ejecutado",
                    color = NeonRed.copy(alpha = 0.5f),
                    fontSize = 9.sp,
                    modifier = Modifier
                        .background(
                            color = SurfaceVariantDark,
                            shape = RoundedCornerShape(8.dp)
                        )
                        .border(1.dp, NeonRedBorder, RoundedCornerShape(8.dp))
                        .padding(horizontal = 8.dp, vertical = 3.dp)
                )
            }
        }
    }
}

fun Modifier.glowEffect(color: Color, blurRadius: Dp): Modifier = drawBehind {
    drawIntoCanvas { canvas ->
        val paint = Paint().apply {
            asFrameworkPaint().apply {
                isAntiAlias = true
                this.color = android.graphics.Color.TRANSPARENT
                setShadowLayer(blurRadius.toPx(), 0f, 0f, color.toArgb())
                maskFilter = BlurMaskFilter(blurRadius.toPx(), BlurMaskFilter.Blur.NORMAL)
            }
        }
        canvas.drawRoundRect(0f, 0f, size.width, size.height, 18.dp.toPx(), 18.dp.toPx(), paint)
    }
}
```

- [ ] **Step 2: Crear `TypingIndicator.kt`**

```kotlin
// ui/components/TypingIndicator.kt
package com.ares.mobile.ui.components

import androidx.compose.animation.core.*
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.scale
import androidx.compose.ui.unit.dp
import com.ares.mobile.ui.theme.*

@Composable
fun TypingIndicator() {
    val infiniteTransition = rememberInfiniteTransition(label = "typing")

    @Composable
    fun dot(delayMs: Int): Float {
        val scale by infiniteTransition.animateFloat(
            initialValue = 0.4f,
            targetValue = 1f,
            animationSpec = infiniteRepeatable(
                animation = keyframes {
                    durationMillis = 1200
                    0.4f at 0
                    1f at 300 + delayMs
                    0.4f at 600 + delayMs
                    0.4f at 1200
                },
                repeatMode = RepeatMode.Restart
            ),
            label = "dot_$delayMs"
        )
        return scale
    }

    Box(
        modifier = Modifier
            .background(SurfaceVariantDark, RoundedCornerShape(18.dp))
            .border(1.dp, NeonRedBorder, RoundedCornerShape(18.dp))
            .padding(horizontal = 16.dp, vertical = 12.dp)
    ) {
        Row(
            horizontalArrangement = Arrangement.spacedBy(5.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            listOf(0, 200, 400).forEach { delay ->
                val scale = dot(delay)
                Box(
                    modifier = Modifier
                        .size(7.dp)
                        .scale(scale)
                        .background(NeonRed, CircleShape)
                )
            }
        }
    }
}
```

- [ ] **Step 3: Crear `QuickActionsBar.kt`**

```kotlin
// ui/components/QuickActionsBar.kt
package com.ares.mobile.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.ares.mobile.ui.theme.*

data class QuickAction(val emoji: String, val label: String, val toolInvocation: String)

private val DEFAULT_ACTIONS = listOf(
    QuickAction("📷", "Foto", "Toma una foto y analízala"),
    QuickAction("📋", "Clipboard", "¿Qué hay en el portapapeles?"),
    QuickAction("📍", "Ubicación", "¿Cuál es mi ubicación actual?"),
    QuickAction("⏰", "Alarma", "Ponme una alarma para las "),
    QuickAction("🎤", "Voz", "Di en voz alta: ")
)

@Composable
fun QuickActionsBar(
    onAction: (String) -> Unit,
    modifier: Modifier = Modifier
) {
    LazyRow(
        modifier = modifier,
        horizontalArrangement = Arrangement.spacedBy(7.dp),
        contentPadding = PaddingValues(horizontal = 16.dp, vertical = 6.dp)
    ) {
        items(DEFAULT_ACTIONS) { action ->
            Row(
                modifier = Modifier
                    .background(SurfaceDark, RoundedCornerShape(16.dp))
                    .border(1.dp, NeonRedBorder, RoundedCornerShape(16.dp))
                    .clickable { onAction(action.toolInvocation) }
                    .padding(horizontal = 12.dp, vertical = 7.dp),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(5.dp)
            ) {
                Text(action.emoji, fontSize = 13.sp)
                Text(action.label, color = NeonRed, fontSize = 11.sp)
            }
        }
    }
}
```

- [ ] **Step 4: Commit**

```bash
git add app/src/main/java/com/ares/mobile/ui/components/
git commit -m "feat(mobile): add MessageBubble with glow effect, TypingIndicator, QuickActionsBar"
```

---

## Task 12: FirstRunScreen

**Files:**
- Create: `ui/screens/FirstRunScreen.kt`

- [ ] **Step 1: Crear `FirstRunScreen.kt`**

```kotlin
// ui/screens/FirstRunScreen.kt
package com.ares.mobile.ui.screens

import android.Manifest
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.animation.core.*
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import com.ares.mobile.ai.DownloadState
import com.ares.mobile.ai.ModelManager
import com.ares.mobile.ai.ModelRouter
import com.ares.mobile.ui.theme.*
import kotlinx.coroutines.launch
import javax.inject.Inject

private val REQUIRED_PERMISSIONS = arrayOf(
    Manifest.permission.CAMERA,
    Manifest.permission.RECORD_AUDIO,
    Manifest.permission.ACCESS_FINE_LOCATION
)

@Composable
fun FirstRunScreen(
    modelManager: ModelManager,
    onSetupComplete: () -> Unit
) {
    val scope = rememberCoroutineScope()
    var phase by remember { mutableStateOf(FirstRunPhase.PERMISSIONS) }
    var downloadState by remember { mutableStateOf<DownloadState>(DownloadState.Idle) }
    var permissionsGranted by remember { mutableStateOf(false) }

    val permissionLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions()
    ) { results ->
        permissionsGranted = results.values.all { it }
        if (permissionsGranted) phase = FirstRunPhase.DOWNLOAD
    }

    val selectedModel = remember {
        ModelRouter.selectModel(modelManager.availableRamBytes(), null)
    }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(BackgroundDeep),
        contentAlignment = Alignment.Center
    ) {
        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.spacedBy(24.dp),
            modifier = Modifier
                .fillMaxWidth()
                .padding(32.dp)
        ) {
            // Logo
            Text(
                text = "⬡ ARES",
                color = NeonRed,
                fontSize = 32.sp,
                fontWeight = FontWeight.ExtraBold,
                letterSpacing = 6.sp
            )
            Text(
                text = "Asistente de IA personal\non-device con Gemma 4",
                color = TextSecondary,
                fontSize = 14.sp,
                textAlign = TextAlign.Center
            )

            Spacer(modifier = Modifier.height(8.dp))

            when (phase) {
                FirstRunPhase.PERMISSIONS -> PermissionsStep(
                    onRequest = { permissionLauncher.launch(REQUIRED_PERMISSIONS) }
                )
                FirstRunPhase.DOWNLOAD -> DownloadStep(
                    modelName = selectedModel.displayName,
                    downloadState = downloadState,
                    onStart = {
                        scope.launch {
                            modelManager.downloadModel(selectedModel).collect { state ->
                                downloadState = state
                                if (state is DownloadState.Complete) {
                                    phase = FirstRunPhase.DONE
                                }
                            }
                        }
                    }
                )
                FirstRunPhase.DONE -> DoneStep(onContinue = onSetupComplete)
            }
        }
    }
}

private enum class FirstRunPhase { PERMISSIONS, DOWNLOAD, DONE }

@Composable
private fun PermissionsStep(onRequest: () -> Unit) {
    Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Text("Permisos necesarios", color = TextPrimary, fontWeight = FontWeight.SemiBold)
        listOf(
            "📷 Cámara — para análisis de imágenes",
            "🎤 Micrófono — para comandos de voz",
            "📍 Ubicación — para el asistente de ubicación"
        ).forEach { Text(it, color = TextSecondary, fontSize = 13.sp, textAlign = TextAlign.Center) }
        Spacer(modifier = Modifier.height(8.dp))
        AresButton("Conceder permisos", onClick = onRequest)
    }
}

@Composable
private fun DownloadStep(modelName: String, downloadState: DownloadState, onStart: () -> Unit) {
    Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(14.dp)) {
        Text("Descargar modelo", color = TextPrimary, fontWeight = FontWeight.SemiBold)
        Text("$modelName (~1.5 GB)", color = TextSecondary, fontSize = 13.sp)
        Text("Se descarga una sola vez. Después funciona offline.", color = TextMuted, fontSize = 11.sp, textAlign = TextAlign.Center)

        when (downloadState) {
            is DownloadState.Idle -> AresButton("Descargar Gemma 4", onClick = onStart)
            is DownloadState.Downloading -> {
                Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    LinearProgressIndicator(
                        progress = { downloadState.progress },
                        modifier = Modifier.fillMaxWidth().height(4.dp),
                        color = NeonRed,
                        trackColor = SurfaceDark
                    )
                    val mb = downloadState.downloadedBytes / 1_000_000
                    val total = downloadState.totalBytes / 1_000_000
                    Text("$mb MB / $total MB", color = TextSecondary, fontSize = 11.sp)
                }
            }
            is DownloadState.Verifying -> {
                Text("Verificando integridad...", color = NeonRed, fontSize = 12.sp)
                CircularProgressIndicator(color = NeonRed, modifier = Modifier.size(24.dp))
            }
            is DownloadState.Complete -> Text("✓ Descarga completa", color = NeonRed)
            is DownloadState.Error -> Column(horizontalAlignment = Alignment.CenterHorizontally) {
                Text("Error: ${downloadState.message}", color = NeonRed, fontSize = 12.sp)
                Spacer(Modifier.height(8.dp))
                AresButton("Reintentar", onClick = onStart)
            }
        }
    }
}

@Composable
private fun DoneStep(onContinue: () -> Unit) {
    Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Text("✓ Todo listo", color = NeonRed, fontSize = 18.sp, fontWeight = FontWeight.Bold)
        Text("ARES está listo para funcionar completamente offline.", color = TextSecondary, fontSize = 13.sp, textAlign = TextAlign.Center)
        Spacer(Modifier.height(8.dp))
        AresButton("Empezar", onClick = onContinue)
    }
}

@Composable
fun AresButton(text: String, onClick: () -> Unit) {
    Button(
        onClick = onClick,
        colors = ButtonDefaults.buttonColors(
            containerColor = NeonRedDim,
            contentColor = TextAres
        ),
        shape = RoundedCornerShape(12.dp),
        modifier = Modifier.fillMaxWidth()
    ) {
        Text(text, fontWeight = FontWeight.SemiBold)
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add app/src/main/java/com/ares/mobile/ui/screens/FirstRunScreen.kt
git commit -m "feat(mobile): add FirstRunScreen with permission flow and model download progress"
```

---

## Task 13: ChatScreen

**Files:**
- Create: `ui/screens/ChatScreen.kt`

- [ ] **Step 1: Crear `ChatScreen.kt`**

```kotlin
// ui/screens/ChatScreen.kt
package com.ares.mobile.ui.screens

import androidx.compose.animation.*
import androidx.compose.foundation.*
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.Mic
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import com.ares.mobile.ui.components.*
import com.ares.mobile.ui.theme.*
import com.ares.mobile.viewmodel.ChatViewModel

@Composable
fun ChatScreen(viewModel: ChatViewModel = hiltViewModel()) {
    val uiState by viewModel.uiState.collectAsState()
    val listState = rememberLazyListState()
    var inputText by remember { mutableStateOf("") }

    // Auto-scroll al último mensaje
    LaunchedEffect(uiState.messages.size) {
        if (uiState.messages.isNotEmpty()) {
            listState.animateScrollToItem(uiState.messages.size - 1)
        }
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(BackgroundDeep)
    ) {
        // Header
        ChatHeader(
            modelName = uiState.modelName.ifEmpty { "Gemma 4" },
            isGenerating = uiState.isGenerating,
            onClear = { viewModel.clearConversation() }
        )

        // Messages
        LazyColumn(
            state = listState,
            modifier = Modifier
                .weight(1f)
                .fillMaxWidth()
                .padding(horizontal = 16.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp),
            contentPadding = PaddingValues(vertical = 12.dp)
        ) {
            items(uiState.messages, key = { it.id }) { message ->
                if (message.isStreaming && message.content.isEmpty()) {
                    Row(horizontalArrangement = Arrangement.Start, modifier = Modifier.fillMaxWidth()) {
                        TypingIndicator()
                    }
                } else {
                    MessageBubble(message)
                }
            }
        }

        // Quick actions
        QuickActionsBar(onAction = { invocation ->
            inputText = invocation
        })

        // Input area
        ChatInputBar(
            text = inputText,
            onTextChange = { inputText = it },
            isGenerating = uiState.isGenerating,
            onSend = {
                if (inputText.isNotBlank()) {
                    viewModel.sendMessage(inputText.trim())
                    inputText = ""
                }
            },
            onMicClick = { /* VoiceTool STT — implementar con SpeechRecognizer en Fase 1B */ }
        )
    }
}

@Composable
private fun ChatHeader(modelName: String, isGenerating: Boolean, onClear: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(BackgroundDeep)
            .border(bottom = 1.dp, color = NeonRedBorder)
            .padding(horizontal = 20.dp, vertical = 12.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            Box(
                modifier = Modifier
                    .size(34.dp)
                    .background(
                        Brush.linearGradient(listOf(NeonRed, NeonRedDim)),
                        RoundedCornerShape(10.dp)
                    ),
                contentAlignment = Alignment.Center
            ) {
                Text("⬡", fontSize = 18.sp)
            }
            Text(
                "ARES",
                color = NeonRed,
                fontSize = 18.sp,
                fontWeight = FontWeight.ExtraBold,
                letterSpacing = 4.sp
            )
        }

        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(12.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(5.dp)) {
                val dotColor = if (isGenerating) NeonRed else StatusOnline
                Box(modifier = Modifier.size(7.dp).background(dotColor, CircleShape))
                Text(modelName, color = dotColor.copy(alpha = 0.8f), fontSize = 10.sp)
            }
            IconButton(onClick = onClear, modifier = Modifier.size(28.dp)) {
                Icon(Icons.Default.Delete, contentDescription = "Limpiar chat", tint = TextMuted, modifier = Modifier.size(18.dp))
            }
        }
    }
}

@Composable
private fun ChatInputBar(
    text: String,
    onTextChange: (String) -> Unit,
    isGenerating: Boolean,
    onSend: () -> Unit,
    onMicClick: () -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(BackgroundDeep)
            .border(top = 1.dp, color = NeonRedBorder)
            .padding(horizontal = 16.dp, vertical = 10.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(10.dp)
    ) {
        BasicTextField(
            value = text,
            onValueChange = onTextChange,
            modifier = Modifier
                .weight(1f)
                .background(SurfaceDark, RoundedCornerShape(22.dp))
                .border(1.dp, BorderSubtle, RoundedCornerShape(22.dp))
                .padding(horizontal = 16.dp, vertical = 11.dp),
            textStyle = LocalTextStyle.current.copy(color = TextPrimary, fontSize = 13.sp),
            cursorBrush = SolidColor(NeonRed),
            decorationBox = { innerTextField ->
                if (text.isEmpty()) {
                    Text("Escribe o habla...", color = TextSecondary, fontSize = 13.sp)
                }
                innerTextField()
            }
        )

        Box(
            modifier = Modifier
                .size(44.dp)
                .background(
                    Brush.linearGradient(listOf(NeonRed, NeonRedDim)),
                    CircleShape
                )
                .clickable(enabled = !isGenerating) {
                    if (text.isNotBlank()) onSend() else onMicClick()
                },
            contentAlignment = Alignment.Center
        ) {
            Icon(
                imageVector = if (text.isBlank()) Icons.Default.Mic else Icons.Default.Send,
                contentDescription = null,
                tint = androidx.compose.ui.graphics.Color.White,
                modifier = Modifier.size(20.dp)
            )
        }
    }
}

// Extension para border en un solo lado
private fun Modifier.border(bottom: Dp? = null, top: Dp? = null, color: androidx.compose.ui.graphics.Color): Modifier {
    return if (bottom != null) this.drawBehind {
        drawLine(color = color, start = androidx.compose.ui.geometry.Offset(0f, size.height), end = androidx.compose.ui.geometry.Offset(size.width, size.height), strokeWidth = bottom.toPx())
    } else if (top != null) this.drawBehind {
        drawLine(color = color, start = androidx.compose.ui.geometry.Offset(0f, 0f), end = androidx.compose.ui.geometry.Offset(size.width, 0f), strokeWidth = top.toPx())
    } else this
}
```

- [ ] **Step 2: Añadir `import androidx.compose.ui.graphics.drawscope.drawBehind`** al principio de ChatScreen.kt (ya incluido implícitamente en la extensión de Modifier al final del archivo).

- [ ] **Step 3: Commit**

```bash
git add app/src/main/java/com/ares/mobile/ui/screens/ChatScreen.kt
git commit -m "feat(mobile): add ChatScreen with streaming messages, quick actions, input bar"
```

---

## Task 14: Pantallas secundarias (Memory, Tasks, Settings)

**Files:**
- Create: `ui/screens/MemoryScreen.kt`
- Create: `ui/screens/TasksScreen.kt`
- Create: `ui/screens/SettingsScreen.kt`

- [ ] **Step 1: Crear `MemoryScreen.kt`**

```kotlin
// ui/screens/MemoryScreen.kt
package com.ares.mobile.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import com.ares.mobile.ui.theme.*
import com.ares.mobile.viewmodel.MemoryViewModel

@Composable
fun MemoryScreen(viewModel: MemoryViewModel = hiltViewModel()) {
    val memories by viewModel.memories.collectAsState()
    var showClearDialog by remember { mutableStateOf(false) }

    Column(
        modifier = Modifier.fillMaxSize().background(BackgroundDeep).padding(16.dp)
    ) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Text("Memoria", color = TextPrimary, fontSize = 18.sp, fontWeight = FontWeight.SemiBold)
            if (memories.isNotEmpty()) {
                TextButton(onClick = { showClearDialog = true }) {
                    Text("Borrar todo", color = NeonRed, fontSize = 12.sp)
                }
            }
        }

        Spacer(modifier = Modifier.height(12.dp))

        if (memories.isEmpty()) {
            Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                Text("Sin recuerdos guardados", color = TextMuted, fontSize = 13.sp)
            }
        } else {
            LazyColumn(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                items(memories, key = { it.key }) { memory ->
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .background(SurfaceDark, RoundedCornerShape(10.dp))
                            .border(1.dp, BorderSubtle, RoundedCornerShape(10.dp))
                            .padding(12.dp),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Column(modifier = Modifier.weight(1f)) {
                            Text(memory.key, color = NeonRed, fontSize = 11.sp, fontWeight = FontWeight.Medium)
                            Text(memory.value, color = TextPrimary, fontSize = 13.sp)
                        }
                        IconButton(onClick = { viewModel.delete(memory.key) }, modifier = Modifier.size(28.dp)) {
                            Icon(Icons.Default.Delete, contentDescription = "Borrar", tint = TextMuted, modifier = Modifier.size(16.dp))
                        }
                    }
                }
            }
        }
    }

    if (showClearDialog) {
        AlertDialog(
            onDismissRequest = { showClearDialog = false },
            title = { Text("Borrar toda la memoria", color = TextPrimary) },
            text = { Text("¿Seguro? ARES olvidará todo lo que sabe sobre ti.", color = TextSecondary) },
            confirmButton = {
                TextButton(onClick = { viewModel.clearAll(); showClearDialog = false }) {
                    Text("Borrar", color = NeonRed)
                }
            },
            dismissButton = {
                TextButton(onClick = { showClearDialog = false }) {
                    Text("Cancelar", color = TextSecondary)
                }
            },
            containerColor = SurfaceDark
        )
    }
}
```

- [ ] **Step 2: Crear `TasksScreen.kt`**

```kotlin
// ui/screens/TasksScreen.kt
package com.ares.mobile.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import com.ares.mobile.data.ScheduledTaskEntity
import com.ares.mobile.ui.theme.*
import com.ares.mobile.viewmodel.TasksViewModel
import java.text.SimpleDateFormat
import java.util.*

@Composable
fun TasksScreen(viewModel: TasksViewModel = hiltViewModel()) {
    val tasks by viewModel.tasks.collectAsState()
    val fmt = remember { SimpleDateFormat("dd/MM HH:mm", Locale.getDefault()) }

    Column(
        modifier = Modifier.fillMaxSize().background(BackgroundDeep).padding(16.dp)
    ) {
        Text("Tareas programadas", color = TextPrimary, fontSize = 18.sp, fontWeight = FontWeight.SemiBold)
        Spacer(modifier = Modifier.height(12.dp))

        if (tasks.isEmpty()) {
            Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                Text("Sin tareas programadas\nDile a ARES que cree una alarma", color = TextMuted, fontSize = 13.sp)
            }
        } else {
            LazyColumn(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                items(tasks, key = { it.id }) { task ->
                    TaskRow(task = task, formatted = fmt.format(Date(task.triggerAtMillis)), onDelete = { viewModel.delete(task.id) })
                }
            }
        }
    }
}

@Composable
private fun TaskRow(task: ScheduledTaskEntity, formatted: String, onDelete: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(SurfaceDark, RoundedCornerShape(10.dp))
            .border(1.dp, BorderSubtle, RoundedCornerShape(10.dp))
            .padding(12.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Column(modifier = Modifier.weight(1f)) {
            Text(formatted, color = NeonRed, fontSize = 11.sp, fontWeight = FontWeight.Medium)
            Text(task.description, color = TextPrimary, fontSize = 13.sp)
        }
        TextButton(onClick = onDelete) {
            Text("Cancelar", color = TextMuted, fontSize = 11.sp)
        }
    }
}
```

Añadir `TasksViewModel.kt` en el paquete `viewmodel`:

```kotlin
// viewmodel/TasksViewModel.kt
package com.ares.mobile.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.ares.mobile.data.ScheduledTaskDao
import com.ares.mobile.data.ScheduledTaskEntity
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

@HiltViewModel
class TasksViewModel @Inject constructor(
    private val taskDao: ScheduledTaskDao
) : ViewModel() {
    private val _tasks = MutableStateFlow<List<ScheduledTaskEntity>>(emptyList())
    val tasks: StateFlow<List<ScheduledTaskEntity>> = _tasks.asStateFlow()

    init { load() }

    private fun load() {
        viewModelScope.launch { _tasks.value = taskDao.getAll() }
    }

    fun delete(id: Long) {
        viewModelScope.launch {
            taskDao.delete(id)
            load()
        }
    }
}
```

- [ ] **Step 3: Crear `SettingsScreen.kt`**

```kotlin
// ui/screens/SettingsScreen.kt
package com.ares.mobile.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import com.ares.mobile.ai.GemmaModel
import com.ares.mobile.ui.theme.*
import com.ares.mobile.viewmodel.SettingsViewModel

@Composable
fun SettingsScreen(viewModel: SettingsViewModel = hiltViewModel()) {
    val settings by viewModel.settings.collectAsState()

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(BackgroundDeep)
            .verticalScroll(rememberScrollState())
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(20.dp)
    ) {
        Text("Configuración", color = TextPrimary, fontSize = 18.sp, fontWeight = FontWeight.SemiBold)

        SettingsSection(title = "Modelo") {
            Text("Modelo activo", color = TextPrimary, fontSize = 13.sp)
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                GemmaModel.entries.forEach { model ->
                    val isSelected = settings.forceModel == model || (settings.forceModel == null && model == GemmaModel.E2B)
                    FilterChip(
                        selected = isSelected,
                        onClick = { viewModel.setForceModel(model) },
                        label = { Text(model.displayName, fontSize = 11.sp) },
                        colors = FilterChipDefaults.filterChipColors(
                            selectedContainerColor = NeonRedDim,
                            selectedLabelColor = TextAres,
                            containerColor = SurfaceDark,
                            labelColor = TextSecondary
                        )
                    )
                }
            }

            Spacer(Modifier.height(4.dp))
            Text("Máx. tokens: ${settings.maxTokens}", color = TextSecondary, fontSize = 12.sp)
            Slider(
                value = settings.maxTokens.toFloat(),
                onValueChange = { viewModel.setMaxTokens(it.toInt()) },
                valueRange = 256f..2048f,
                steps = 7,
                colors = SliderDefaults.colors(thumbColor = NeonRed, activeTrackColor = NeonRed, inactiveTrackColor = SurfaceElevated)
            )
        }

        SettingsSection(title = "Respuestas") {
            SettingsToggle("Leer respuestas en voz alta", settings.ttsEnabled, onToggle = viewModel::setTtsEnabled)
            SettingsToggle("Modo razonamiento (thinking)", settings.thinkingMode, onToggle = viewModel::setThinkingMode)
        }
    }
}

@Composable
private fun SettingsSection(title: String, content: @Composable ColumnScope.() -> Unit) {
    Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
        Text(title, color = NeonRed, fontSize = 11.sp, letterSpacing = 1.sp)
        content()
    }
}

@Composable
private fun SettingsToggle(label: String, value: Boolean, onToggle: (Boolean) -> Unit) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text(label, color = TextPrimary, fontSize = 13.sp)
        Switch(
            checked = value,
            onCheckedChange = onToggle,
            colors = SwitchDefaults.colors(checkedThumbColor = NeonRed, checkedTrackColor = NeonRedDim)
        )
    }
}
```

- [ ] **Step 4: Commit**

```bash
git add app/src/main/java/com/ares/mobile/ui/screens/ app/src/main/java/com/ares/mobile/viewmodel/TasksViewModel.kt
git commit -m "feat(mobile): add Memory, Tasks, Settings screens"
```

---

## Task 15: Navegación y MainActivity

**Files:**
- Create: `ui/navigation/AppNavigation.kt`
- Create: `MainActivity.kt`

- [ ] **Step 1: Crear `AppNavigation.kt`**

```kotlin
// ui/navigation/AppNavigation.kt
package com.ares.mobile.ui.navigation

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.navigation.NavDestination.Companion.hierarchy
import androidx.navigation.compose.*
import com.ares.mobile.ai.ModelManager
import com.ares.mobile.ui.screens.*
import com.ares.mobile.ui.theme.*

private sealed class Screen(val route: String, val label: String, val icon: ImageVector) {
    object Chat : Screen("chat", "Chat", Icons.Default.Chat)
    object Memory : Screen("memory", "Memoria", Icons.Default.Psychology)
    object Tasks : Screen("tasks", "Tareas", Icons.Default.Schedule)
    object Settings : Screen("settings", "Config", Icons.Default.Settings)
}

private val bottomNavItems = listOf(Screen.Chat, Screen.Memory, Screen.Tasks, Screen.Settings)

@Composable
fun AppNavigation(modelManager: ModelManager) {
    val navController = rememberNavController()
    val isFirstRun = remember { !modelManager.isModelDownloaded(
        com.ares.mobile.ai.ModelRouter.selectModel(modelManager.availableRamBytes(), null)
    )}

    if (isFirstRun) {
        var setupDone by remember { mutableStateOf(false) }
        if (!setupDone) {
            FirstRunScreen(modelManager = modelManager, onSetupComplete = { setupDone = true })
            return
        }
    }

    Scaffold(
        containerColor = BackgroundDeep,
        bottomBar = {
            NavigationBar(
                containerColor = BackgroundDeep,
                tonalElevation = 0.dp
            ) {
                val navBackStackEntry by navController.currentBackStackEntryAsState()
                val currentDestination = navBackStackEntry?.destination
                bottomNavItems.forEach { screen ->
                    val selected = currentDestination?.hierarchy?.any { it.route == screen.route } == true
                    NavigationBarItem(
                        selected = selected,
                        onClick = { navController.navigate(screen.route) { launchSingleTop = true; restoreState = true } },
                        icon = {
                            Icon(
                                screen.icon,
                                contentDescription = screen.label,
                                tint = if (selected) NeonRed else TextMuted
                            )
                        },
                        label = { Text(screen.label, fontSize = 9.sp, color = if (selected) NeonRed else TextMuted) },
                        colors = NavigationBarItemDefaults.colors(
                            indicatorColor = SurfaceVariantDark,
                            selectedIconColor = NeonRed
                        )
                    )
                }
            }
        }
    ) { innerPadding ->
        NavHost(
            navController = navController,
            startDestination = Screen.Chat.route,
            modifier = Modifier.padding(innerPadding).background(BackgroundDeep)
        ) {
            composable(Screen.Chat.route) { ChatScreen() }
            composable(Screen.Memory.route) { MemoryScreen() }
            composable(Screen.Tasks.route) { TasksScreen() }
            composable(Screen.Settings.route) { SettingsScreen() }
        }
    }
}
```

- [ ] **Step 2: Crear `MainActivity.kt`**

```kotlin
// MainActivity.kt
package com.ares.mobile

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.systemBarsPadding
import androidx.compose.ui.Modifier
import com.ares.mobile.ai.ModelManager
import com.ares.mobile.ui.navigation.AppNavigation
import com.ares.mobile.ui.theme.AresTheme
import com.ares.mobile.ui.theme.BackgroundDeep
import dagger.hilt.android.AndroidEntryPoint
import javax.inject.Inject

@AndroidEntryPoint
class MainActivity : ComponentActivity() {

    @Inject lateinit var modelManager: ModelManager

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            AresTheme {
                androidx.compose.foundation.layout.Box(
                    modifier = Modifier
                        .fillMaxSize()
                        .background(BackgroundDeep)
                        .systemBarsPadding()
                ) {
                    AppNavigation(modelManager = modelManager)
                }
            }
        }
    }
}
```

- [ ] **Step 3: Build y verificar que compila**

  En Android Studio: Build → Make Project (`Ctrl+F9`).  
  Expected: BUILD SUCCESSFUL sin errores de compilación.

- [ ] **Step 4: Ejecutar en emulador (API 26+)**

  Run → Run 'app'. Verifica:
  - La app abre en `FirstRunScreen` si el modelo no está descargado
  - El tema rojo neón se aplica correctamente
  - La bottom navigation funciona entre las 4 tabs

- [ ] **Step 5: Commit final**

```bash
git add app/src/main/java/com/ares/mobile/
git commit -m "feat(mobile): add navigation, MainActivity — ARES Mobile Phase 1 complete"
```

---

## Notas de integración importantes

### URLs del modelo
Las URLs de descarga de Gemma 4 en `ModelRouter.kt` son placeholders. Antes de ejecutar:
1. Consulta [ai.google.dev/edge/mediapipe/solutions/genai/llm_inference/android](https://ai.google.dev/edge/mediapipe/solutions/genai/llm_inference/android) para las URLs oficiales de los modelos `.task`
2. Actualiza los campos `downloadUrl` y `sha256` en el enum `GemmaModel`

### Verificación de hash SHA-256
Los hashes están como placeholder. Una vez tengas los archivos `.task`, genera el hash con:
```bash
sha256sum gemma4-e2b-it-int4.task
```

### CameraTool
La integración de CameraX para captura real requiere un `ActivityResultContract` en la Activity. El diseño actual de `CameraTool` usa el patrón de "señal" (`CAMERA_NEEDED`) que la UI debe manejar. Conectar la señal `ChatUiState.needsCameraCapture = true` con `ActivityResultContracts.TakePicture()` en `ChatScreen` es el paso pendiente para completar la integración de cámara.

### LiteRT-LM API
La API de `LlmInference.generateResponseAsync` en `GemmaClient.kt` usa la firma de MediaPipe Tasks GenAI 0.10.x. Si la versión tiene una firma diferente, ajusta los lambdas del callback (algunos releases usan `ProgressListener<String>` en lugar de dos lambdas separadas).
