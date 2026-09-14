# Matriz de capacidades de Sakura

> Diseño D3.2. Inventario capacidad por capacidad. Estados permitidos únicamente:
> **Implementado**, **Parcial**, **Infraestructura disponible**, **Planeado**, **Investigación**,
> **Fuera de alcance**. Nada se marca como implementado si no existe hoy en
> `release/kohana-1.0-rc`. Ver también `docs/roadmap/SAKURA_TECHNOLOGY_ROADMAP.md` (fases) y
> `docs/architecture/SAKURA_CAPABILITY_ARCHITECTURE.md` (capas).

| Capacidad | Descripción | Estado | Infraestructura disponible | Dependencias | Local/Híbrido/Cloud | Hardware | Permisos | Riesgo | Superficie | Fase | Sprint sugerido |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Sakura Shell | Ventana principal, navegación de 9 secciones | Implementado | — | — | Local | CPU/GPU básicos | Ninguno | Bajo | App principal | Fase 0 | D1/D1.1 |
| Command Center | Paleta de comandos Ctrl+K, búsqueda y ejecución | Implementado | — | Sakura Shell | Local | — | Ninguno | Bajo | Command Center | Fase 0 | D2 |
| Daily Flow | Tareas, Hoy, Rutinas conectadas | Implementado | — | Sakura Shell | Local | — | Ninguno | Bajo | App principal | Fase 0 | D3 |
| Focus Continuity | Mini temporizador, historial, refresco inmediato | Implementado | — | Daily Flow | Local | — | Ninguno | Bajo | App principal | Fase 0 | D3.1 |
| Validation Data Sandbox | Aislamiento de datos para validación (`KOHANA_DATA_ROOT`) | Implementado | — | — | Local | — | Ninguno (uso interno/dev) | Bajo | — (infraestructura) | Fase 0 | D3.2 |
| Voice Runtime | Coordinación de Whisper + Vosk + SAPI | Implementado | — | — | Local | CPU (Whisper), micrófono | Micrófono | Medio | App principal | Fase 0 | D1 |
| Engine Registry | Catálogo y estado de motores reales | Implementado | — | Voice Runtime, proveedores de IA | Local/Híbrido/Cloud según motor | — | Ninguno | Bajo | Sistema (vista) | Fase 0 | Fase 2.2 (previo a D1) |
| Hardware Profile | Detección de capacidad de hardware | Implementado | — | — | Local | CPU/RAM/GPU/batería | Ninguno | Bajo | Sistema (vista) | Fase 0 | Fase 2.1 (previo a D1) |
| Sakura Pills | Ventanas ambientales sin robo de foco | Planeado | — | Ambient Surface Host | Local | — | Primitivas de permisos | Bajo | Sakura Pills | Fase 1 | D4 |
| Lens | Observar y explicar contexto visual | Investigación | Captura de pantalla (`IScreenCaptureService`) | Ambient Surface Host, OCR, VLM | Local/Híbrido | GPU recomendada para VLM | Captura de pantalla, ventana activa | Alto | Lens Overlay | Fase 2 | Lens: captura y OCR |
| Flow | Dictado global | Investigación | Whisper (Voice Runtime) | Voice Bar (Fase 1) | Local | CPU (Whisper) | Micrófono global, inserción de texto | Medio | Voice Bar | Fase 3 | Flow: dictado global v1 |
| OCR | Reconocimiento de texto en pantalla | Investigación | — | Lens | Local/Híbrido | CPU/GPU | Captura de pantalla | Medio | Lens Overlay | Fase 2 | Lens: captura y OCR |
| Vision (VLM) | Modelo visual para analizar región/pantalla | Investigación | — | Lens | Híbrido/Cloud | GPU recomendada | Captura de pantalla | Alto | Lens Overlay | Fase 2 | Lens: guía visual y modos |
| Selected Text Actions | Acciones sobre texto seleccionado | Investigación | — | Flow | Local | — | Portapapeles/selección | Bajo | Voice Bar / Panel lateral | Fase 3 | Flow: dictado global v1 |
| Clipboard Intelligence | Contexto desde portapapeles | Investigación | — | Context Sources | Local | — | Portapapeles | Medio | Panel lateral | Fase 3 | Flow: dictado global v1 |
| Adaptive Optimization | Optimización del equipo por escenario | Investigación | Hardware Profile, Engine Registry | Snapshots y reversión | Local | CPU/RAM/GPU/batería/temperatura | Cambios de configuración del sistema | Alto | App principal | Fase 4 | Optimización: snapshots y reversión |
| Project Companion | Trabajo asistido en proyectos de código | Investigación | — | Modelo de confianza completo, Lens | Local/Híbrido | — | Workspace autorizado | Alto | App principal | Fase 5 | Companion: workspace y modo Guía |
| Workspace Editing | Edición de archivos de proyecto | Investigación | — | Project Companion | Local | — | Workspace autorizado | Alto | App principal | Fase 5 | Companion: workspace y modo Guía |
| Terminal Runtime | Ejecución de comandos de terminal | Investigación | — | Project Companion | Local | — | Workspace autorizado | Alto | App principal | Fase 5 | Companion: modo Agente y checkpoints |
| Git Runtime | Operaciones de control de versiones | Investigación | — | Project Companion | Local | — | Workspace autorizado | Alto | App principal | Fase 5 | Companion: modo Agente y checkpoints |
| Memory | Contexto y memoria persistente | Investigación | — | Controles de retención | Local | — | Retención de datos | Medio | Panel lateral | Fase 6 | Memoria: retención y exclusiones antes que almacenamiento |
| Computer Use | Ejecución de acciones sobre el sistema | Investigación | `Nexo.Core.Automation` (acciones internas únicamente) | Modelo de confianza completo, Permission Broker | Local | — | El más alto del roadmap | Alto | App principal | Fase 7 | Computer Use: niveles Ver/Guiar/Proponer |
| App Actions | Integración con Windows App Actions | Investigación | — | Computer Use | Local | Windows reciente | Según acción | Medio | App principal | Fase 7 | Computer Use: niveles Ver/Guiar/Proponer |
| MCP | Integración con servidores MCP | Investigación | — | Computer Use | Local/Híbrido/Cloud según servidor | — | Según servidor | Medio | App principal | Fase 7 | Computer Use: ejecución y auditoría |
| Skills | Packs de capacidades combinadas | Investigación | — | Fases 1–7 | Según pack | Según pack | Hereda de las capacidades incluidas | Medio | App principal | Fase 8 | Skills: Sakura Study |
| Meeting Assistant | Asistencia en reuniones | Fuera de alcance | Voice Runtime | Skills | Local/Híbrido | Micrófono | Micrófono, posible captura de audio del sistema | Alto | Voice Bar | Fase 8 | Skills: Sakura Meeting |
| Support Mode | Modo de soporte técnico guiado | Investigación | Captura de pantalla | Lens | Local/Híbrido | — | Captura de pantalla | Medio | Lens Overlay | Fase 2/8 | Lens: guía visual y modos |
| Study Mode | Modo de estudio guiado | Investigación | — | Lens | Local/Híbrido | — | Captura de pantalla (opcional) | Bajo | Lens Overlay | Fase 2/8 | Lens: guía visual y modos |
| Creator Mode | Asistencia creativa | Fuera de alcance | — | Skills | Híbrido/Cloud | GPU recomendada | Según capacidad usada | Medio | App principal | Fase 8 | Skills: Sakura Creator |
| Accessibility Mode | Accesibilidad ampliada | Investigación | UI Automation (uso futuro) | Lens, Computer Use | Local | — | UI Automation | Medio | App principal | Fase 8 | Skills: Sakura Access |
| Installer | Instalador de Sakura | Investigación | `scripts/build-installer.ps1` (empaquetado manual) | — | Local | — | Instalación de sistema | Medio | — (infraestructura) | Fase 9 | Productization: instalador y actualizador |
| Updater | Actualizador de Sakura | Investigación | — | Installer | Local | — | Instalación de sistema | Medio | — (infraestructura) | Fase 9 | Productization: instalador y actualizador |

## Notas de lectura

- "Infraestructura disponible" significa que existe una pieza reutilizable de código hoy
  (`IScreenCaptureService`, `Nexo.Core.Automation`, Voice Runtime, scripts de empaquetado) que una
  fase futura extenderá — no que la capacidad completa funcione.
- Ninguna fila de Fase 2 en adelante está marcada "Implementado": estas son, sin excepción,
  capacidades de investigación o planeadas sobre las que este sprint (Diseño D3.2) **no** ha
  escrito código, según instrucción explícita del encargo.
- El nivel de "Riesgo" es el riesgo de la capacidad en su forma final descrita en el roadmap, no
  del trabajo de documentación de este sprint.
