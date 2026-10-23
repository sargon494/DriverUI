# MVP.md

Alcance de la primera versión utilizable de **DriverUpdater**.

## 1. Objetivo del MVP

Demostrar que se puede **detectar e instalar actualizaciones de drivers de forma segura** en Windows 10/11 usando únicamente **Windows Update** como fuente, con un servicio en segundo plano y una interfaz sencilla.

El MVP **no necesita backend**.

## 2. Alcance

### Incluido
- Inventario de drivers instalados.
- Búsqueda de actualizaciones de drivers vía Windows Update Agent.
- Instalación de uno o varios drivers.
- Verificación de firma digital antes de instalar (solo drivers firmados).
- Punto de restauración previo a cada lote de instalaciones.
- Rollback de la última actualización.
- Historial de operaciones y log.
- Servicio de Windows + UI WPF sin privilegios.
- Comprobación programada (diaria/semanal) y notificación.
- CLI de diagnóstico.

### Excluido (fases posteriores)
- Backend, cuentas, licencias, telemetría.
- Fuentes OEM o de fabricantes de componentes (NVIDIA, AMD, Intel).
- Gestión de flotas / consola web.
- Autoactualización de la aplicación.
- Localización a varios idiomas (el MVP en español).

## 3. Historias de usuario

| ID | Como usuario quiero… | Para… |
|---|---|---|
| U1 | ver la lista de drivers instalados con versión y fecha | saber el estado de mi equipo |
| U2 | pulsar "Buscar actualizaciones" y ver cuáles hay | decidir qué instalar |
| U3 | instalar un driver concreto o todos los pendientes | mantener el equipo actualizado |
| U4 | que se cree un punto de restauración antes de instalar | poder volver atrás si algo falla |
| U5 | deshacer la última actualización | recuperar un driver que da problemas |
| U6 | ver un historial de lo que se instaló y cuándo | tener trazabilidad |
| U7 | excluir drivers que no quiero actualizar | evitar cambios en hardware sensible |
| U8 | que la app compruebe actualizaciones sola | no tener que acordarme |

## 4. Criterios de aceptación

- **U1**: el inventario muestra nombre, fabricante, versión, fecha y estado de cada dispositivo; tarda menos de 30 s.
- **U2**: la búsqueda lista solo actualizaciones de tipo driver, con versión actual → nueva.
- **U3**: la instalación muestra progreso, informa de éxito o error por driver y avisa si hace falta reiniciar.
- **U4**: si no se puede crear el punto de restauración, la instalación **no continúa** y se informa al usuario.
- **U5**: tras el rollback, la versión del driver vuelve a la anterior y queda registrado.
- **U6**: el historial persiste tras reiniciar el equipo.
- **U7**: un driver excluido no aparece en actualizaciones ni se instala en lote.
- **U8**: no aplica. Decisión: sin notificaciones de escritorio ni tarea programada por usuario (`DESIGN.md` §13.2). La comprobación programada deja su resultado en el indicador de la app.
- **Seguridad**: un paquete con firma inválida o sin firma es rechazado y registrado.
- **Rendimiento**: servicio en reposo por debajo de 50 MB de RAM.

## 5. Hitos y tareas

### Hito 0: Prototipos (1-2 semanas)
- [ ] Crear solución .NET 10 y proyectos `Core`, `Agent.Drivers`, `Cli`.
- [ ] Listar drivers con `Win32_PnPSignedDriver`.
- [ ] Buscar drivers con WUA (`Type='Driver'`) y mostrar resultados.
- [ ] Instalar un driver de prueba en una VM.
- [ ] Decisión go / no-go: ¿la fuente da resultados útiles?

### Hito 1: Núcleo + CLI (3-4 semanas)
- [ ] Modelos e interfaces en `Core`; comparación de versiones con tests.
- [ ] `IDriverInventory` y `IUpdateSource` (WUA).
- [ ] `ISignatureVerifier` (WinVerifyTrust).
- [ ] `IRestorePointService`.
- [ ] `IDriverInstaller` con instalación, registro y rollback.
- [ ] Historial en SQLite.
- [ ] Comandos CLI: `scan`, `updates`, `install`, `rollback`, `log`.

### Hito 2: Servicio + UI (3-4 semanas)
- [ ] Worker Service como servicio de Windows.
- [ ] Servidor gRPC sobre named pipe con ACL restringida.
- [ ] Cola de operaciones (una instalación a la vez) y cancelación.
- [ ] UI WPF: inicio, lista de actualizaciones, historial, ajustes.
- [ ] Exclusiones y comprobación programada.
- [ ] Aviso de reinicio pendiente (sin notificaciones de escritorio).

### Hito 3: Seguridad y robustez (2-3 semanas)
- [ ] Solo drivers firmados por defecto; opción documentada para el resto.
- [ ] Pruebas en VMs con Windows 10 y 11 y en 2-3 equipos reales.
- [ ] Pruebas de fallo (corte de red, firma inválida, reinicio durante instalación).
- [ ] Revisión de ACL del pipe y validación de entradas.

### Hito 4: Instalador y beta (2 semanas)
- [ ] MSI con WiX que registre servicio y UI.
- [ ] Firma de código del instalador y binarios.
- [ ] Beta cerrada con pocos usuarios y recogida de feedback.

## 6. Definición de "hecho" para el MVP

- Todos los criterios de aceptación cumplidos.
- Probado en Windows 10 y 11 (VM + hardware real).
- Sin warnings en `Release`; tests unitarios en verde.
- Instalador firmado que instala y desinstala sin dejar restos.
- Documentación mínima de usuario (instalar, escanear, actualizar, deshacer).

## 7. Riesgos del MVP

| Riesgo | Impacto | Mitigación |
|---|---|---|
| Windows Update ofrece pocos drivers útiles | Alto | Validar en el Hito 0; si es pobre, adelantar fuentes OEM |
| Driver que causa pantallazo azul | Alto | Punto de restauración, rollback, instalación una a una, pruebas en VM |
| Falsos positivos de antivirus | Medio | Firma de código, instalador firmado |
| Complejidad de COM/WUA en .NET | Medio | Prototipo temprano; aislar tras `IUpdateSource` |
| Falta de variedad de hardware para pruebas | Medio | VMs con snapshots y beta con voluntarios |

## 8. Después del MVP

1. Backend con catálogo propio y listas de drivers problemáticos.
2. Fuentes OEM y de fabricantes (previa revisión de licencias).
3. Autoactualización y canales stable/beta.
4. Cuentas, licencias y telemetría opcional.
5. Versión para empresas con consola de gestión.
