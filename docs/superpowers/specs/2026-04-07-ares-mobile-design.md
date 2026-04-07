# ARES Mobile — Design Spec
**Fecha:** 2026-04-07  
**Versión:** 1.0  
**Estado:** Aprobado por usuario

---

## Visión general

ARES Mobile es la versión Android nativa del asistente ARES Desktop. Corre Gemma 4 completamente on-device mediante LiteRT-LM (Google AI Edge), sin dependencia de servidores externos ni conexión a internet tras la descarga inicial del modelo. Es una app independiente del desktop, no un cliente remoto.

Se desarrolla en **Kotlin + Jetpack Compose** para máximo rendimiento y acceso directo a las APIs de IA de Android.

---

## Plataforma y stack

| Aspecto | Decisión |
|---|---|
| Plataforma | Android (API 26+, Android 8.0) |
| Lenguaje | Kotlin |
| UI | Jetpack Compose |
| Arquitectura | MVVM (ViewModel + StateFlow) |
| IA on-device | LiteRT-LM (Google AI Edge SDK) |
| Modelo por defecto | Gemma 4 E2B (cuantizado 4-bit, ~1.5 GB) |
| Modelo alternativo | Gemma 4 E4B (~3 GB, si RAM ≥ 6 GB) |
| Base de datos | Room (SQLite) |
| DI | Hilt |

---

## Modelo de IA

### Selección automática de modelo
`ModelManager` comprueba la RAM disponible en el primer arranque:
- RAM libre ≥ 4 GB → descarga E4B (mejor calidad)
- RAM libre < 4 GB → descarga E2B (más ligero)

El usuario puede forzar el modelo en Settings.

### Descarga del modelo
- Se realiza en `FirstRunScreen` con barra de progreso
- Guardado en el directorio interno de la app (no accesible por otras apps)
- Verificación de hash SHA-256 tras descarga
- Una vez descargado, la app funciona 100% offline

### Inferencia
- Streaming de tokens via LiteRT-LM para respuesta en tiempo real
- Context window: 4096 tokens (configurable hasta 8192 en E4B)
- Thinking mode: desactivado por defecto, activable por el usuario en Settings
- Multimodal: imágenes desde cámara o galería se pasan como input junto al texto

---

## Arquitectura

```
ARES Mobile
│
├── UI Layer (Compose)
│   ├── FirstRunScreen      — descarga modelo, permisos iniciales
│   ├── ChatScreen          — conversación principal
│   ├── MemoryScreen        — ver/editar/borrar recuerdos persistentes
│   ├── TasksScreen         — tareas programadas (scheduler)
│   └── SettingsScreen      — modelo, parámetros, permisos, tema
│
├── ViewModel Layer
│   ├── ChatViewModel       — estado de chat, coordina AgentLoop
│   ├── MemoryViewModel     — CRUD sobre MemoryStore
│   └── SettingsViewModel   — configuración de la app
│
├── Agent Layer
│   ├── AgentLoop           — ciclo: prompt → inferencia → tool call → respuesta
│   ├── ToolRegistry        — registro y dispatch de tools
│   └── ConversationHistory — historial en RAM + persistencia Room
│
├── Tools (Fase 1)
│   ├── ClipboardTool       — leer y escribir portapapeles Android
│   ├── CameraTool          — captura foto → input multimodal a Gemma 4
│   ├── VoiceTool           — STT con SpeechRecognizer + TTS con TextToSpeech
│   ├── LocationTool        — coordenadas GPS via FusedLocationProviderClient
│   └── AlarmTool           — crear alarmas y recordatorios via AlarmManager
│
├── AI Layer
│   ├── GemmaClient         — wrapper LiteRT-LM: chat, streaming, tool calls
│   ├── ModelManager        — descarga, verificación hash, gestión espacio
│   └── ModelRouter         — selección E2B/E4B según RAM
│
└── Storage (Room)
    ├── ConversationDao     — historial de mensajes
    ├── MemoryDao           — recuerdos persistentes (clave-valor + embedding)
    └── ScheduledTaskDao    — tareas programadas
```

### Flujo de una conversación
1. Usuario escribe texto o habla (VoiceTool → STT)
2. `ChatViewModel` añade mensaje al historial y llama a `AgentLoop`
3. `AgentLoop` construye el prompt con historial + system prompt + tools disponibles
4. `GemmaClient` inicia streaming contra LiteRT-LM; tokens aparecen en tiempo real en `ChatScreen`
5. Si la respuesta contiene un tool call, el streaming se pausa, `ToolRegistry` ejecuta la tool
6. El resultado se añade al historial y se relanza la inferencia con el contexto completo
7. La respuesta final se muestra y opcionalmente se lee en voz alta (TTS)

### Function calling
LiteRT-LM con Gemma 4 soporta function calling nativo. Cada tool expone un `ToolDefinition` (nombre, descripción, parámetros JSON Schema) idéntico al patrón del desktop. El `AgentLoop` inyecta las definiciones en el prompt de sistema y parsea la respuesta estructurada.

---

## UI y estética

### Paleta de color
| Token | Valor | Uso |
|---|---|---|
| `colorPrimary` | `#FF2020` | Botones, glow, activos |
| `colorAccent` | `#8B0000` | Gradientes, sombras |
| `colorBackground` | `#050505` | Fondo general |
| `colorSurface` | `#0D0D0D` | Cards, input |
| `colorSurfaceVariant` | `#110000` | Burbujas IA |
| `colorOnSurface` | `#CCCCCC` | Texto usuario |
| `colorOnSurfaceVariant` | `#FF9090` | Texto IA |
| `colorBorder` | `#1F1F1F` | Bordes sutiles |
| `colorBorderGlow` | `#FF20201A` | Bordes con glow |

### Tipografía
- UI general: `Roboto` (system font Android)
- Monospace (badges de tool, status): `Roboto Mono`
- Tamaño base: 13sp para mensajes

### Efectos
- Glow rojo sutil en burbujas del asistente (`box-shadow` equivalente en Compose: `drawBehind` con `BlurMaskFilter`)
- Indicador pulsante del modelo activo en status bar
- Animación typing (3 puntos rebotando) mientras se genera respuesta
- Scrollbar fino (3dp) semitransparente en color primario

### Navegación — Bottom Navigation Bar
| Sección | Icono | Descripción |
|---|---|---|
| Chat | 💬 | Conversación principal |
| Memoria | 🧠 | Recuerdos persistentes del usuario |
| Tareas | ⏱️ | Scheduler: tareas programadas |
| Config | ⚙️ | Modelo, parámetros, permisos |

### ChatScreen — elementos
- **Header:** Logo `⬡ ARES` + nombre modelo activo + uso RAM
- **Lista de mensajes:** LazyColumn con burbujas usuario (derecha, oscuro) y ARES (izquierda, rojo oscuro + glow)
- **Tool badge:** chip pequeño debajo de la burbuja IA indicando la tool ejecutada
- **Quick actions:** fila horizontal deslizable con chips para invocar tools directamente
- **Input:** TextField redondeado + botón micrófono con glow rojo
- **Typing indicator:** 3 puntos animados durante generación

---

## Permisos Android requeridos

| Permiso | Para |
|---|---|
| `CAMERA` | CameraTool |
| `RECORD_AUDIO` | VoiceTool (STT) |
| `ACCESS_FINE_LOCATION` | LocationTool |
| `SCHEDULE_EXACT_ALARM` | AlarmTool |
| `READ_CLIPBOARD` (implícito) | ClipboardTool |

Todos se solicitan en `FirstRunScreen` con explicación contextual antes del diálogo del sistema.

---

## Fase 2 — Extensiones futuras

Fuera del scope de esta versión, planificadas para fases posteriores:

1. **Más tools móviles:** ContactsTool, NotificationTool, FilesTool, AppLauncherTool, SMSTool
2. **Bridge con ARES Desktop:** `RemoteBridgeClient` que detecta el PC en la misma red WiFi vía mDNS y se conecta a la `LocalApiServer` del desktop (ya existe en el desktop). Permite enviar comandos al PC desde el móvil.
3. **Sincronización de memoria:** Sincronizar `MemoryStore` entre desktop y mobile via bridge.

---

## Estructura de carpetas del proyecto

```
ARES-mobile/
├── app/
│   ├── src/main/
│   │   ├── java/com/ares/mobile/
│   │   │   ├── ui/
│   │   │   │   ├── screens/       — ChatScreen, MemoryScreen, etc.
│   │   │   │   ├── components/    — MessageBubble, TypingIndicator, etc.
│   │   │   │   └── theme/         — Color.kt, Theme.kt, Type.kt
│   │   │   ├── viewmodel/
│   │   │   ├── agent/             — AgentLoop, ToolRegistry
│   │   │   ├── tools/             — ClipboardTool, CameraTool, etc.
│   │   │   ├── ai/                — GemmaClient, ModelManager, ModelRouter
│   │   │   ├── data/              — Room DB, DAOs, entities
│   │   │   └── di/                — Hilt modules
│   │   └── res/
│   └── build.gradle.kts
└── build.gradle.kts
```

---

## Dependencias principales (build.gradle.kts)

```kotlin
// UI
implementation("androidx.compose.ui:ui")
implementation("androidx.compose.material3:material3")
implementation("androidx.navigation:navigation-compose:2.7.x")

// AI on-device
implementation("com.google.ai.edge.litert:litert-lm:1.x.x")

// Architecture
implementation("androidx.hilt:hilt-android:2.x")
implementation("androidx.room:room-runtime:2.6.x")
implementation("androidx.lifecycle:lifecycle-viewmodel-compose:2.7.x")

// JSON
implementation("org.jetbrains.kotlinx:kotlinx-serialization-json:1.6.x")
```

---

## Criterios de éxito (Fase 1)

- [ ] El modelo Gemma 4 E2B corre on-device sin crash en un dispositivo con 6 GB RAM
- [ ] Respuesta de primera generación en < 3 segundos en hardware mid-range
- [ ] Streaming visible token a token en `ChatScreen`
- [ ] Las 5 tools funcionan correctamente (clipboard, cámara, voz, ubicación, alarma)
- [ ] La memoria persiste entre sesiones (Room)
- [ ] La app funciona 100% offline tras la descarga del modelo
