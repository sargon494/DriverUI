# Arquitectura de DriverUpdater

Guía para entender el proyecto sin leer carpeta por carpeta. Complementa a:

- `MVP.md`: alcance y criterios de aceptación.
- `DESIGN.md`: decisiones técnicas y el detalle de cada sprint.
- `AGENTS.md`: reglas de seguridad, convenciones de código y cómo trabajar.
- `SPRINT_LOG.md`: histórico de sprints.

## 1. Qué es

Aplicación para Windows 10/11 que busca actualizaciones de drivers oficiales (Windows Update) y las instala de forma segura: solo drivers firmados, con punto de restauración previo y registro de operaciones.

Está dividida en dos procesos que hablan entre sí:

- **Servicio** (`Agent.Service`): corre como Windows Service con cuenta SYSTEM. Es el único que toca el sistema: WMI, Windows Update, `pnputil`, puntos de restauración y firmas.
- **UI** (`Ui.Wpf`): aplicación WPF sin privilegios de administrador. Solo muestra información y envía órdenes al servicio.

La comunicación es **gRPC sobre un named pipe local** (`\\.\pipe\DriverUpdater`).

```
┌──────────────────────────┐   gRPC / named pipe    ┌──────────────────────────────┐
│ Ui.Wpf (usuario)         │ ─────────────────────▶ │ Agent.Service (SYSTEM)       │
│ MVVM, sin privilegios    │ ◀───────────────────── │ gRPC server + cola + tareas  │
└──────────────────────────┘                        └──────────────┬───────────────┘
        │ usa                                                     │ usa
        ▼                                                         ▼
  Agent.Ipc (contrato .proto + canal cliente)            Agent.Drivers (WMI, WUA, pnputil,
                                                         firmas, restore points, SQLite)
                                                                  │
                                                                  ▼
                                                              Core (modelos e interfaces)
```

## 2. Proyectos

| Proyecto | Responsabilidad | Depende de |
|---|---|---|
| `src/Core` | Modelos (`InstalledDriver`, `DriverUpdate`, `AppSettings`…), interfaces (`IDriverInventory`, `IUpdateSource`, `IDriverInstaller`, `ISettingsStore`…) y lógica pura: `VersionComparer`, `DriverMatcher`, `OemSupportCatalog`. **Sin dependencias de Windows.** | nada |
| `src/Agent.Drivers` | Implementaciones que tocan Windows: inventario WMI, Windows Update (COM), instalación vía WUA, puntos de restauración, verificación de firma (WinVerifyTrust), historial en SQLite. | `Core` |
| `src/Agent.Ipc` | Contrato gRPC (`Protos/driveragent.proto`) y `DriverAgentChannel`, la fábrica del canal cliente sobre el named pipe. Lo comparten servicio y UI. | nada |
| `src/Agent.Service` | Servidor gRPC, cola de operaciones, comprobación programada, ajustes persistentes, ACL del pipe. | `Core`, `Agent.Drivers`, `Agent.Ipc` |
| `src/Ui.Wpf` | Interfaz WPF en MVVM (CommunityToolkit.Mvvm). Cliente del servicio. | `Core`, `Agent.Ipc` |
| `src/Cli` | Herramienta de diagnóstico sin UI (`scan`, `updates`). Usa `Agent.Drivers` directamente, sin servicio. | `Core`, `Agent.Drivers` |
| `tests/Core.Tests` | Tests unitarios de la lógica pura. | `Core` |
| `tests/Agent.Drivers.Tests` | Tests de las implementaciones; los de integración van marcados con `[Trait("Category","Integration")]`. | `Agent.Drivers` |
| `tests/Agent.Service.Tests` | Tests del servicio: levantan un host gRPC real sobre pipe con dobles de las interfaces. | `Agent.Service` |
| `installer/` | Instalador MSI (WiX v4), script de build y guía de prueba. | — |

`Core` es el centro: no sabe nada de Windows ni de gRPC, y todo lo demás depende de él. Así la lógica se prueba sin máquina Windows.

## 3. Servicio (`Agent.Service`)

### Arranque (`Program.cs`)

1. Configura Kestrel para escuchar en el named pipe `DriverUpdater`, con HTTP/2 y gRPC puro.
2. Aplica la ACL del pipe (`NamedPipeSecurityFactory`): solo usuarios locales interactivos pueden conectarse.
3. Registra las dependencias en DI:
   - `IOperationQueue` → `OperationQueue` (singleton).
   - `ISettingsStore` → `JsonSettingsStore`.
   - `ScheduledCheckState` (singleton) y `ScheduledCheckService` (servicio en segundo plano).
   - Las implementaciones de `Agent.Drivers` mediante `AddDriverServices()`.
4. Mapea `DriverAgentGrpcService`.

### Piezas principales

- **`DriverAgentGrpcService`**: implementa todos los RPC. Traduce mensajes gRPC a modelos de `Core` y viceversa (`GrpcMappers`). Cada operación que toca el sistema se encola.
- **`OperationQueue`**: serializa las operaciones con un semáforo. Una búsqueda y una instalación nunca se solapan. Expone el estado (`OperationState`): operación en curso, porcentaje, fase, último escaneo, pendientes en cola.
- **`ScheduledCheckService`**: cada 15 minutos mira si toca comprobar. Según `CheckIntervalHours` (0, 24 o 168) busca actualizaciones pasando por la **misma cola** que la UI, y guarda el resultado en `ScheduledCheckState` (memoria). No arranca con Windows: solo corre mientras el servicio esté en marcha.
- **`JsonSettingsStore`**: lee y escribe `%ProgramData%\DriverUpdater\config.json`. Escritura atómica (fichero temporal + reemplazo). Si el fichero está dañado usa valores por defecto. Normaliza y valida antes de guardar (`AppSettings.Normalize`).

### RPC expuestos

| RPC | Qué hace |
|---|---|
| `Scan` | Inventario de drivers instalados (WMI). |
| `CheckUpdates` | Stream con las actualizaciones disponibles. Filtra las excluidas por el usuario. |
| `Install` | Stream de progreso de instalación. Rechaza las excluidas. Crea punto de restauración antes. |
| `Rollback` | Revierte una operación registrada en el historial. |
| `GetHistory` | Registro de operaciones (SQLite). |
| `GetStatus` | Estado de la cola, último escaneo, reinicio pendiente y resultado de la última comprobación programada. |
| `GetSettings` / `UpdateSettings` | Lee y guarda los ajustes persistentes. |
| `GetMotherboard` | Fabricante y modelo de la placa (`Win32_BaseBoard`). |

## 4. Interfaz (`Ui.Wpf`)

### Estructura MVVM

- **`App.xaml.cs`**: arranque y DI. Registra `DriverAgentService` y los ViewModels como singleton.
- **`ShellWindow`** + **`ShellViewModel`**: ventana principal. Navegación entre páginas (`AppPage`), estado de conexión (`IsConnected`, `IsConnecting`) y sondeo del servicio cada 5 segundos. Si no hay conexión, muestra la pantalla de reintento.
- **Páginas**: `DashboardViewModel`/`DashboardView` (inventario agrupado por clase), `UpdatesViewModel`/`UpdatesView` (buscar, seleccionar, instalar, excluir), `HistoryViewModel`/`HistoryView`, `SettingsViewModel`/`SettingsView`.
- **`DriverAgentService`**: única puerta de la UI hacia el servicio. Envuelve el cliente gRPC, gestiona el canal y controla el servicio de Windows (`ServiceController`) para arrancarlo bajo demanda.

### Flujo de conexión

1. `ShellViewModel.InitializeAsync` → `ConnectOrStartAsync`.
2. Intenta conectar. Si falla, arranca el servicio con `ServiceController` y reintenta hasta 5 segundos (el servicio figura como "Running" antes de tener el pipe listo).
3. Si conecta, carga la página activa y empieza el sondeo.

El servicio **no** arranca con Windows: el instalador lo registra en modo manual (`Start="demand"`) y concede a usuarios interactivos el permiso de iniciarlo y detenerlo (`sc sdset`).

### Estilos y recursos

- `Resources/Colors.xaml`: paleta (tokens de color y tipografía).
- `Resources/Styles.xaml`: estilos de botones, tarjetas, expanders y animaciones. Incluye `Icons.xaml` mediante `MergedDictionaries`.
- `Resources/Icons.xaml`: geometrías SVG como `Geometry`.

## 5. Flujos de datos clave

### Buscar actualizaciones (pantalla Actualizaciones)

```
UpdatesViewModel.CheckUpdatesAsync
  → DriverAgentService.CheckUpdatesAsync (stream gRPC)
    → DriverAgentGrpcService.CheckUpdates
      → OperationQueue.EnqueueAsync
        → IDriverInventory.GetInstalledDriversAsync   (WMI)
        → IUpdateSource.FindUpdatesAsync               (WUA)
        → filtro de exclusiones (AppSettings)
  ← DriverUpdateMsg por cada actualización
```

### Instalar

```
UpdatesViewModel.InstallSelectedAsync
  → Install (stream de progreso)
    → OperationQueue
      → IDriverInstaller (WuaDriverInstaller), que:
          · crea el punto de restauración (WmiRestorePointService) y aborta si falla
          · instala vía WUA, con verificación de firma (ISignatureVerifier / WinVerifyTrust) en la fase Verifying
      → IOperationHistory.RecordAsync (SQLite)
  ← InstallProgressMsg (fase, porcentaje, resultado)
```

### Excluir una actualización

```
UpdatesViewModel.ExcludeAsync
  → GetSettings → añade el hardware ID → UpdateSettings
  → la lista se actualiza; las siguientes búsquedas ya no la muestran
```

### Comprobación programada y aviso

```
ScheduledCheckService (cada 15 min)
  → si toca: EnqueueAsync(buscar actualizaciones)
  → ScheduledCheckState.Record(hora, pendientes)
GetStatus devuelve esos datos
ShellViewModel.PollStatusAsync → UpdatesBadge (solo para una comprobación nueva)
```

### Soporte de la placa (fase A)

```
DashboardViewModel → GetMotherboard → Win32_BaseBoard
OemSupportCatalog.Find(fabricante) → URL oficial HTTPS
El botón solo abre el navegador. No se descarga nada.
```

## 6. Almacenamiento

| Dato | Dónde | Quién escribe |
|---|---|---|
| Ajustes | `%ProgramData%\DriverUpdater\config.json` | Servicio (`JsonSettingsStore`) |
| Historial de operaciones | `%ProgramData%\DriverUpdater\history.db` (SQLite) | Servicio |
| Logs del servicio | `%ProgramData%\DriverUpdater\logs\service-*.log` | Servicio (Serilog) |
| Logs de la UI | `%ProgramData%\DriverUpdater\logs\ui-*.log` | UI (Serilog) |

La UI nunca escribe en `%ProgramData%`: pide los cambios al servicio.

## 7. Seguridad (resumen)

Las reglas completas están en `AGENTS.md`. Las que definen la arquitectura:

- El privilegio vive en el servicio. La UI nunca se ejecuta elevada.
- El pipe solo acepta usuarios locales. La ACL se configura al crear el pipe.
- Ningún driver se instala sin verificar su firma. Por defecto solo WHQL o firmados por Microsoft.
- Siempre hay punto de restauración antes de instalar. Si no se puede crear, no se instala.
- Las URL de soporte son HTTPS y pertenecen a la lista de `OemSupportCatalog`.
- No hay llamadas de red en el producto, salvo las que describe `DESIGN.md`.

## 8. Cómo extender

### Añadir un RPC nuevo

1. Define el mensaje y el `rpc` en `src/Agent.Ipc/Protos/driveragent.proto`.
2. Implementa el método en `DriverAgentGrpcService` (si toca el sistema, pásalo por `OperationQueue`).
3. Si necesita lógica de dominio, añade la interfaz en `Core/Interfaces` y la implementación en `Agent.Drivers`.
4. Añade el método envoltorio en `Ui.Wpf/Services/DriverAgentService.cs`.
5. Añade tests en `tests/Agent.Service.Tests` (con el harness, ver §9).

### Añadir una página a la UI

1. Crea `ViewModels/XxxViewModel.cs` heredando de `PageViewModelBase` y regístralo en `App.xaml.cs`.
2. Crea `Views/XxxView.xaml` (UserControl) y su `DataTemplate` en `ShellWindow.xaml`.
3. Añade el valor a `AppPage` y el `NavItem` en la barra lateral.

### Añadir una fuente de drivers

Implementa `IUpdateSource` en `Agent.Drivers` y registrarla en `ServiceCollectionExtensions`. Fuentes OEM (fase B) están pendientes de la revisión de licencias (`DESIGN.md` §12).

## 9. Pruebas

- **`Core.Tests`**: lógica pura, rápidos, sin Windows.
- **`Agent.Service.Tests`**: `AgentHarness` levanta un host gRPC real sobre un pipe con los dobles de `Agent.Drivers` (NSubstitute). **Cada nueva dependencia del servicio debe registrarse en los dos harness** (`AgentHarness` y el de la ACL del pipe), o todos los RPC fallan con `Exception was thrown by handler`. Los dobles de ajustes y placa están en `SettingsStubs`.
- **`Agent.Drivers.Tests`**: tests de integración marcados con `[Trait("Category","Integration")]`.
- **UI**: no tiene tests automáticos. Se prueba en una VM con cuenta estándar.

El CI (`.github/workflows/ci.yml`) compila y ejecuta los tests en `windows-latest`. Si un test falla, cada test fallido aparece como anotación en el run.

## 10. Build, instalador y release

- `installer/build.ps1 -Version X.Y.Z`: publica el servicio y la UI como single-file autocontenidos (`win-x64`) y genera el MSI con WiX.
- `IncludeNativeLibrariesForSelfExtract=true` es necesario en el servicio: sin él, SQLite no encuentra su DLL nativa.
- CI: el job `build-installer` genera el MSI en cada push a `master`. `release.yml` crea la release al subir un tag `v*`.

## 11. Trampas conocidas

- **XAML**: `StaticResource` solo ve las claves de su propio diccionario y de los `MergedDictionaries`. Las duraciones de `Storyboard` dentro de estilos deben ser literales (`0:0:0.15`), no `DynamicResource`: rompe el sellado de los estilos al cargar la ventana.
- **Naming**: `.editorconfig` exige `_camelCase` para todos los campos privados, también los `static readonly`. Las advertencias cuentan como error en CI.
- **ACL del servicio**: `sc sdset` reemplaza toda la ACL. Si solo concedes permisos a usuarios interactivos, deja también la consulta de estado (`CCLCSWRPWPDTLOCRRC`) o la UI no podrá leer el estado.
- **Windows Update**: un mismo driver puede aparecer varias veces en `Win32_PnPSignedDriver` (una por instancia del dispositivo). No es un error.
- **Puntos de restauración**: si la Restauración del sistema está desactivada, la instalación falla. En la VM: `Enable-ComputerRestore -Drive "C:\"`.
