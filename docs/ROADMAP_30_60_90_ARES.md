# ARES Roadmap 30/60/90 (Abril 2026)

## Objetivo
Convertir ARES en un asistente local robusto, reparable en un clic, con automatizaciones confiables y UX clara para uso diario.

## Criterios de priorizacion
- Impacto usuario: cuanto reduce friccion real.
- Riesgo tecnico: probabilidad de romper flujos existentes.
- Esfuerzo: complejidad de implementacion y QA.
- Dependencias: si desbloquea otras mejoras.

---

## 0-30 dias (Quick Wins + Estabilidad)

### 1) Reparacion IA unificada (routing + vision)
- Prioridad: P0
- Esfuerzo: M
- Riesgo: Bajo
- Resultado esperado:
  - Diagnostico con acciones directas (copiar, instalar faltantes, reparar todo).
  - Re-diagnostico automatico tras reparacion.
- KPI:
  - >95% de reparaciones exitosas en una pasada.
  - Tiempo medio de recuperacion < 3 min.

### 2) Health Check profundo de Ollama
- Prioridad: P0
- Esfuerzo: M
- Riesgo: Bajo
- Alcance:
  - Estados: no instalado, instalado/no corriendo, API no responde, modelos faltantes.
  - Sugerencias accionables por estado.
- KPI:
  - Reduccion de errores de arranque IA en 70%.

### 3) Copia de diagnosticos y logs utiles
- Prioridad: P1
- Esfuerzo: S
- Riesgo: Bajo
- Alcance:
  - Boton "Copiar diagnostico" estandarizado.
  - Adjuntar version app, modelo activo, timestamp, estado herramientas.
- KPI:
  - Tiempo de soporte/reporte reducido en 50%.

### 4) Guardrails de UX para modales largos
- Prioridad: P1
- Esfuerzo: S
- Riesgo: Bajo
- Alcance:
  - Limites visuales, scroll consistente, acciones primarias claras.
- KPI:
  - 0 reportes de modal "gigante" o contenido inaccesible.

### 5) Telemetria local de fiabilidad (opt-in)
- Prioridad: P1
- Esfuerzo: M
- Riesgo: Bajo
- Alcance:
  - Exitos/fallos por herramienta y por modelo.
  - Dashboard interno de salud.
- KPI:
  - Top 3 causas de fallo identificadas semanalmente.

---

## 31-60 dias (Productividad + Automatizacion)

### 6) Perfilado por contexto (trabajo/estudio/gaming)
- Prioridad: P0
- Esfuerzo: M
- Riesgo: Medio
- Alcance:
  - Cambia modelo, tono, autonomia y widgets segun perfil.
- KPI:
  - 40% menos cambios manuales de ajustes.

### 7) Automatizaciones con simulacion previa
- Prioridad: P0
- Esfuerzo: L
- Riesgo: Medio
- Alcance:
  - Modo "simular" antes de ejecutar acciones reales.
  - Historial de ejecucion con rollback basico.
- KPI:
  - 0 acciones destructivas no intencionales.

### 8) Memoria por proyecto
- Prioridad: P1
- Esfuerzo: M
- Riesgo: Medio
- Alcance:
  - Separar contexto por carpeta/repositorio.
  - Limpieza/TTL por proyecto.
- KPI:
  - Mejora percibida de relevancia en respuestas > 30%.

### 9) Router adaptativo por hardware en tiempo real
- Prioridad: P1
- Esfuerzo: M
- Riesgo: Medio
- Alcance:
  - Ajuste automatico de modelo si sube RAM/CPU.
- KPI:
  - Congelamientos UI por inferencia reducidos en 60%.

### 10) Flujo "Reparar todo" desde Ajustes y primer inicio
- Prioridad: P1
- Esfuerzo: S
- Riesgo: Bajo
- Alcance:
  - Mismo flujo en onboarding y settings.
- KPI:
  - Disminucion de abandonos en setup inicial.

---

## 61-90 dias (Escalado + Plataforma)

### 11) Sistema de plugins/herramientas extensible
- Prioridad: P0
- Esfuerzo: L
- Riesgo: Alto
- Alcance:
  - Contrato estable para herramientas externas.
  - Permisos por herramienta.
- KPI:
  - Integrar 5+ plugins sin tocar core.

### 12) Motor de politicas de seguridad local
- Prioridad: P0
- Esfuerzo: M
- Riesgo: Medio
- Alcance:
  - Reglas allow/deny para comandos, archivos y red.
  - Confirmacion reforzada en acciones peligrosas.
- KPI:
  - 0 ejecuciones de alto riesgo sin confirmacion.

### 13) Benchmark automatico de modelos por tarea
- Prioridad: P1
- Esfuerzo: L
- Riesgo: Medio
- Alcance:
  - Medir latencia, calidad y tasa de tool-calls correctos.
  - Recomendacion de modelo por tipo de trabajo.
- KPI:
  - Mejora de calidad percibida +20% con misma latencia.

### 14) Editor visual de flujos de automatizacion
- Prioridad: P1
- Esfuerzo: L
- Riesgo: Medio
- Alcance:
  - Constructor tipo bloques para triggers/acciones.
- KPI:
  - 50% de automatizaciones creadas sin editar JSON.

### 15) API local de ARES
- Prioridad: P2
- Esfuerzo: M
- Riesgo: Medio
- Alcance:
  - Endpoint local para invocar diagnosticos/acciones.
- KPI:
  - Integracion con 2 herramientas externas.

---

## Backlog priorizado (Top 12)
1. Health check Ollama detallado (P0)
2. Reparar todo IA unificado (P0)
3. Simulacion de automatizaciones (P0)
4. Perfiles por contexto (P0)
5. Politicas de seguridad local (P0)
6. Memoria por proyecto (P1)
7. Router adaptativo por hardware (P1)
8. Benchmark de modelos (P1)
9. Telemetria local opt-in (P1)
10. Editor visual de flujos (P1)
11. API local de integracion (P2)
12. Marketplace de plugins local (P2)

---

## Plan de entrega sugerido (sprints de 2 semanas)
- Sprint 1:
  - Health check Ollama + reporte accionable.
  - Harden de modales y copy diagnostico estandar.
- Sprint 2:
  - Reparar todo IA end-to-end.
  - Telemetria local basica de fallos.
- Sprint 3:
  - Perfiles por contexto + ajustes automaticos.
  - Memoria por proyecto (MVP).
- Sprint 4:
  - Simulacion de automatizaciones + historial ejecutable.

---

## Definicion de hecho (DoD)
- Build Release sin errores.
- Flujo cubierto por pruebas manuales guiadas.
- Logs claros para soporte.
- Mensajes de error accionables para usuario final.
- No degradacion de rendimiento percibido en arranque.

---

## Estado de ejecucion (2026-04-02) — Primera mitad del roadmap

Se ejecutaron los items 1 al 8 en formato MVP funcional y conectado al flujo real de la app.

### 1) Reparacion IA unificada (routing + vision)
- Estado: HECHO (MVP)
- Evidencia:
  - Diagnostico con acciones directas: copiar, instalar faltantes y reparar todo IA.
  - Re-diagnostico automatico tras reparacion.
- Archivos clave:
  - `AresAssistant/Views/SettingsWindow.xaml.cs`
  - `AresAssistant/Views/AresMessageBox.xaml.cs`

### 2) Health Check profundo de Ollama
- Estado: HECHO (MVP)
- Evidencia:
  - Nuevo chequeo de salud con estados: no instalado, instalado/no corriendo, API no disponible, modelos faltantes.
  - Sugerencias accionables incluidas en diagnostico.
- Archivos clave:
  - `AresAssistant/Core/OllamaHealthChecker.cs`
  - `AresAssistant/Views/SettingsWindow.xaml.cs`

### 3) Copia de diagnosticos y logs utiles
- Estado: HECHO
- Evidencia:
  - Boton de copiado de diagnostico.
  - Diagnostico incluye timestamp, version app, modelo base y estado de herramientas criticas.
- Archivos clave:
  - `AresAssistant/Views/AresMessageBox.xaml.cs`
  - `AresAssistant/Views/SettingsWindow.xaml.cs`

### 4) Guardrails de UX para modales largos
- Estado: HECHO
- Evidencia:
  - Modal con limites visuales y scroll consistente para contenido extenso.
- Archivos clave:
  - `AresAssistant/Views/AresMessageBox.xaml`

### 5) Telemetria local de fiabilidad (opt-in)
- Estado: HECHO (MVP)
- Evidencia:
  - Telemetria local para exito/fallo y latencia por herramienta y por modelo.
  - Resumen semanal de top fallos integrado en diagnostico.
- Archivos clave:
  - `AresAssistant/Core/ReliabilityTelemetryStore.cs`
  - `AresAssistant/Tools/ToolDispatcher.cs`
  - `AresAssistant/Core/AgentLoop.cs`
  - `AresAssistant/Views/MainWindow.xaml.cs`
  - `AresAssistant/Config/AppConfig.cs`

### 6) Perfilado por contexto (trabajo/estudio/gaming)
- Estado: HECHO (MVP)
- Evidencia:
  - Aplicacion automatica de presets por perfil al guardar ajustes.
  - Ajusta personalidad, longitud, autonomia y widgets segun perfil.
- Archivos clave:
  - `AresAssistant/ViewModels/SettingsViewModel.cs`
  - `AresAssistant/Config/AppConfig.cs`

### 7) Automatizaciones con simulacion previa
- Estado: HECHO (MVP)
- Evidencia:
  - Nueva herramienta `schedule_simulate` para validacion sin ejecucion real.
  - Evalua proxima ejecucion y nivel de permiso/riesgo.
- Archivos clave:
  - `AresAssistant/Tools/ScheduleSimulateTool.cs`
  - `AresAssistant/Views/MainWindow.xaml.cs`
  - `AresAssistant/Core/AgentLoop.cs`

### 8) Memoria por proyecto
- Estado: HECHO (MVP)
- Evidencia:
  - Memoria persistente ahora soporta ambito de proyecto y TTL opcional.
  - Herramientas memory_read/write/forget actualizadas para usar proyecto.
  - Prompt del agente consume memoria por ambito de proyecto.
- Archivos clave:
  - `AresAssistant/Core/PersistentMemoryStore.cs`
  - `AresAssistant/Tools/MemoryWriteTool.cs`
  - `AresAssistant/Tools/MemoryReadTool.cs`
  - `AresAssistant/Tools/MemoryForgetTool.cs`
  - `AresAssistant/Core/AgentLoop.cs`

### Nota de alcance
- Esta entrega cubre la primera mitad en nivel MVP productivo.
- Pendiente para iteracion siguiente: rollback avanzado de automatizaciones (item 7, parte L).

---

## Estado de ejecucion (2026-04-02) — Segunda mitad del roadmap

Se ejecutaron los items 9 al 15 en formato MVP funcional con integracion en runtime.

### 9) Router adaptativo por hardware en tiempo real
- Estado: HECHO (MVP)
- Evidencia:
  - Reordenado de candidatos de modelo bajo presion alta de CPU/RAM para priorizar modelos ligeros.
- Archivos clave:
  - `AresAssistant/Core/RuntimeModelAdvisor.cs`
  - `AresAssistant/Core/ModelRouter.cs`

### 10) Flujo "Reparar todo" desde Ajustes y primer inicio
- Estado: HECHO (MVP)
- Evidencia:
  - Ajustes: flujo completo de reparar todo IA.
  - Onboarding: boton "Reparar IA" en la instalacion inicial para espejo funcional del flujo.
- Archivos clave:
  - `AresAssistant/Views/SettingsWindow.xaml.cs`
  - `AresAssistant/Views/SetupWindow.xaml`
  - `AresAssistant/Views/SetupWindow.xaml.cs`

### 11) Sistema de plugins/herramientas extensible
- Estado: HECHO (MVP)
- Evidencia:
  - Cargador de plugins desde `data/plugins/*.json`.
  - Tipo soportado: `external-command` con herramientas `plugin_*`.
- Archivos clave:
  - `AresAssistant/Core/PluginToolLoader.cs`
  - `AresAssistant/Tools/ExternalCommandPluginTool.cs`
  - `AresAssistant/Views/MainWindow.xaml.cs`

### 12) Motor de politicas de seguridad local
- Estado: HECHO (MVP)
- Evidencia:
  - Motor de politica JSON local (`data/security-policy.json`) integrado en permisos.
  - Soporta bloqueo/confirmacion por herramienta, patrones de comando y prefijos de ruta.
- Archivos clave:
  - `AresAssistant/Core/SecurityPolicyStore.cs`
  - `AresAssistant/Core/PermissionManager.cs`
  - `AresAssistant/Views/MainWindow.xaml.cs`

### 13) Benchmark automatico de modelos por tarea
- Estado: HECHO (MVP)
- Evidencia:
  - Nueva herramienta `model_benchmark` para medir latencia y salida basica por tarea.
- Archivos clave:
  - `AresAssistant/Tools/ModelBenchmarkTool.cs`
  - `AresAssistant/Views/MainWindow.xaml.cs`

### 14) Editor visual de flujos de automatizacion
- Estado: HECHO (MVP)
- Evidencia:
  - Ventana "Automation Studio" con alta/eliminacion/listado/simulacion de tareas programadas.
- Archivos clave:
  - `AresAssistant/Views/AutomationStudioWindow.xaml`
  - `AresAssistant/Views/AutomationStudioWindow.xaml.cs`
  - `AresAssistant/Views/SettingsWindow.xaml`
  - `AresAssistant/Views/SettingsWindow.xaml.cs`

### 15) API local de ARES
- Estado: HECHO (MVP)
- Evidencia:
  - Servidor HTTP local (127.0.0.1) con endpoints: `/health`, `/tools`, `/version`.
- Archivos clave:
  - `AresAssistant/Core/LocalApiServer.cs`
  - `AresAssistant/Views/MainWindow.xaml.cs`

### Nota de alcance
- Segunda mitad completada en nivel MVP productivo.
- Pendiente para iteracion siguiente: ampliar API con endpoints de accion autenticados y versionado de plugins.
