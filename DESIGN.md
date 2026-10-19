# DESIGN.md

Diseño técnico de **DriverUpdater**.

## 1. Objetivos y no objetivos

**Objetivos**
- Detectar drivers desactualizados en Windows 10/11 e instalar versiones nuevas de forma segura.
- Seguridad por defecto: solo drivers firmados, punto de restauración previo, rollback.
- Bajo consumo de recursos en segundo plano.
- Arquitectura que permita añadir fuentes de drivers y un backend más adelante.

**No objetivos (de momento)**
- Soporte para Linux o macOS.
- Redistribuir drivers de terceros desde servidores propios.
- Gestión de flotas empresariales (consola web).

## 2. Decisión de stack

Se evaluaron cuatro opciones:

| Opción | Valoración |
|---|---|
| Todo Java | Buen backend, pero el agente local depende de JNA/Panama para cada API de Windows, runtime empaquetado de 40-60 MB y más RAM en un proceso siempre activo. |
| Híbrido (agente .NET + backend Java) | Válido, pero dos lenguajes, dos pipelines y modelos duplicados son un coste alto para un equipo pequeño. |
| **Todo C# / .NET** | **Elegida.** Acceso nativo a Windows, un solo lenguaje, código compartido entre agente, UI y backend. Sintaxis muy cercana a Java. |
| Rust en el agente | Más ligero, pero curva alta y el cuello de botella no es el rendimiento. |

Java sigue siendo una alternativa razonable para el backend si crece mucho o lo toma otro equipo; la API HTTP del backend es independiente del cliente.

## 3. Arquitectura

```
┌─────────────────────┐   gRPC / named pipe   ┌──────────────────────────┐
│ UI (WPF, usuario)   │ ◄───────────────────► │ Servicio Windows (SYSTEM)│
│ sin privilegios     │                       │ - inventario (WMI)       │
└─────────────────────┘                       │ - WUA / pnputil          │
                                              │ - restore point, rollback│
                                              │ - verificación de firmas │
                                              └───────────┬──────────────┘
                                                          │ HTTPS (TLS)  [fase posterior]
                                              ┌───────────▼──────────────┐
                                              │ Backend ASP.NET Core     │
                                              │ catálogo, cuentas,       │
                                              │ telemetría · PostgreSQL  │
                                              └──────────────────────────┘
```

### Principio central
La **UI no tiene privilegios**; el **servicio hace todo lo peligroso**. La UI solo envía órdenes ("escanear", "instalar el driver X") por un canal local restringido. Así se evita pedir UAC en cada acción y se reduce la superficie de ataque.

## 4. Componentes

### 4.1 `Core`
Sin dependencias de Windows. Contiene:
- Modelos: `InstalledDriver`, `DriverUpdate`, `HardwareId`, `InstallResult`, `OperationLogEntry`.
- Comparación de versiones de driver y lógica de selección (mejor candidato por hardware ID).
- Interfaces: `IDriverInventory`, `IUpdateSource`, `IDriverInstaller`, `ISignatureVerifier`, `IRestorePointService`.

### 4.2 `Agent.Drivers`
Implementaciones Windows de las interfaces de `Core`:
- **Inventario**: WMI `Win32_PnPSignedDriver` (nombre, fabricante, versión, fecha, `HardwareID`, `DeviceID`, `InfName`).
- **Fuente de actualizaciones**: Windows Update Agent (COM `Microsoft.Update.Session`, criterio `Type='Driver'`). Fuente principal del MVP.
- **Instalación**: vía WUA (`IUpdateInstaller`) o `pnputil /add-driver <inf> /install` cuando el paquete ya está descargado.
- **Firmas**: WinVerifyTrust por P/Invoke; comprobación de cadena y de que el catálogo es WHQL/Microsoft.
- **Restore points**: `Checkpoint-Computer` / WMI `SystemRestore`.
- **Rollback**: `pnputil /delete-driver` del nuevo paquete y reinstalación del anterior, o restauración del punto creado.

### 4.3 `Agent.Service`
- Worker Service con `UseWindowsService()`.
- Servidor gRPC en named pipe `\\.\pipe\DriverUpdater` con ACL limitada a usuarios interactivos.
- Cola de operaciones: **una instalación a la vez**, cancelable y con estado persistente.
- Planificador de comprobaciones periódicas (configurable).
- Manejo de reinicios pendientes (`RebootRequired`).

### 4.4 `Ui.Wpf`
- **Patrón**: MVVM con `CommunityToolkit.Mvvm` (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`).
- **Arquitectura de ventanas**: Ventana principal `ShellWindow` sin bordes estándar (*frameless chrome*), controles de ventana integrados, barra de navegación lateral con contadores (*badges*) reactivos y área de contenido dinámica mapeada mediante `DataTemplate` a los ViewModels correspondientes.
- **Pantallas**:
  - *Dashboard (Inicio)*: Métricas globales de hardware, buscador reactivo, pill de salud del sistema y tabla de controladores instalados.
  - *Actualizaciones*: Lista con selección múltiple/individual, comparación de versiones actual vs. nueva, barra de progreso global y por controlador en tiempo real vía streaming gRPC, indicador de necesidad de reinicio y botón de cancelación.
  - *Historial*: Auditoría persistente de instalaciones y rollbacks con filtrado y reversión directa del controlador seleccionado.
  - *Ajustes*: Configuración de intervalo de comprobación periódica, política de controladores firmados (WHQL) y lista de exclusiones por identificador.
- **Seguridad**: Ejecución obligatoria en contexto de usuario estándar (no elevado); no invoca APIs de Windows privilegiadas y toda interacción pasa por `Agent.Ipc` hacia el servicio Windows.
- **Resiliencia gRPC**: `DriverAgentService` maneja reconexión automática ante caídas del servicio y sincronización reactiva de estado (`GetStatus`).
- **Experiencia de cancelación**: Si se cancela una instalación durante la fase nativa, la UI entra en estado diferido (*"Cancelando... esperando a que finalice el controlador en curso"*) hasta que el servicio confirma la liberación segura de la cola.

### 4.5 `Cli`
Herramienta de desarrollo y diagnóstico: `scan`, `updates`, `install <id>`, `rollback <opId>`, `log`. Reutiliza `Agent.Drivers` directamente.

### 4.6 `Agent.Ipc`
Proyecto compartido con el **contrato gRPC** (`Protos/driveragent.proto`, generado con `Grpc.Services=Both`) y la **fábrica de canal cliente** (`DriverAgentChannel.CreateChannel`) que conecta por named pipe mediante `SocketsHttpHandler.ConnectCallback`. Lo consumen el servicio (base del servidor), la UI (cliente) y los tests de integración IPC.

### 4.7 `Backend.Api` (fase posterior)
- ASP.NET Core minimal API + EF Core + PostgreSQL.
- Catálogo de drivers por hardware ID, versiones, compatibilidad, listas de bloqueo (drivers conocidos como problemáticos).
- Cuentas, licencias y telemetría opcional.
- El agente funciona sin backend; este añade valor, no es un requisito.

## 5. Contrato IPC (gRPC)

Contrato en `src/Agent.Ipc/Protos/driveragent.proto` (namespace C# `Agent.Ipc.Grpc`):

```proto
service DriverAgent {
  rpc Scan (ScanRequest) returns (ScanResponse);
  rpc CheckUpdates (CheckUpdatesRequest) returns (stream DriverUpdateMsg);
  rpc Install (InstallRequest) returns (stream InstallProgressMsg);
  rpc Rollback (RollbackRequest) returns (RollbackResultMsg);
  rpc GetHistory (HistoryRequest) returns (HistoryResponse);
  rpc GetStatus (StatusRequest) returns (StatusResponse);
}
```

El streaming permite mostrar progreso en la UI en tiempo real.

Implementación:
- **Transporte**: transporte nativo de named pipes de Kestrel (`ListenNamedPipe`), HTTP/2 en claro (sin TLS: el pipe es local y su ACL restringe el acceso).
- **ACL del pipe**: SYSTEM y Administradores con control completo; usuarios interactivos (SID `S-1-5-4`) con permisos de lectura y escritura (`ReadWrite`). Esto evita conceder `WRITE_DAC` o `WRITE_OWNER` a usuarios interactivos sin privilegios para que no puedan modificar la ACL del objeto. Sin `CurrentUserOnly` (incompatible con ACL explícita y bloquearía a los usuarios de sesiones interactivas, ya que el servicio corre como SYSTEM). *Nota de verificación*: La prueba de extremo a extremo con una cuenta interactiva estándar (no administradora) en VM para certificar la conexión, lectura/escritura y denegación de modificación de ACL queda agendada para el entorno VM.
- **Frontera de seguridad de identidad**: toda la frontera de autorización descansa en la ACL del pipe (`S-1-5-4` local); el servicio no comprueba la identidad individual del usuario en cada RPC porque cualquier sesión interactiva local está autorizada para solicitar comprobaciones e instalaciones (conforme a la regla 4 de `AGENTS.md`).
- **Instalación segura**: `Install` recibe solo identificadores de actualización; el servicio **re-resuelve** cada ID contra Windows Update en el momento de instalar, de modo que el cliente no puede inyectar metadatos de driver manipulados.
- **Progreso en tiempo real desacoplado**: la transmisión de eventos de progreso se realiza en tiempo real a través de un `Channel<T>` con lector concurrente hacia el stream gRPC.
- **Transmisión de `CheckUpdates`**: La búsqueda WUA es una operación COM bloqueante; se ejecuta dentro de la cola serializada y, una vez finalizada la consulta, las actualizaciones encontradas se transmiten al stream gRPC fuera del semáforo para que clientes lentos no retengan la cola.
- **Cancelación y experiencia de usuario (UI)**: Si el usuario cancela una instalación durante las fases de descarga, verificación o creación de punto de restauración, la operación se cancela de inmediato y se audita el fallo. Si la cancelación se solicita cuando el driver ya ha iniciado la fase de instalación nativa (`WUA`/`pnputil`), por seguridad e integridad del subsistema de controladores no se interrumpe y se espera a que dicho driver finalice, cancelando las actualizaciones restantes del lote. La UI (`Ui.Wpf`) debe mostrar en este caso el estado *"Cancelando... esperando a que finalice el controlador en curso"* sin asumir aborto inmediato ni permitir reintentos hasta que el RPC concluya.
- **Política de desconexión del cliente (fallo de transporte)**: Si el cliente gRPC se desconecta de forma abrupta (cierre de la UI, caída del proceso o rotura del pipe durante la transmisión de progreso o resultados):
  - Cualquier pérdida de conexión con el cliente interrumpe de inmediato el trabajo previo a la fase nativa (descarga, verificación o punto de restauración) mediante un token de cancelación enlazado.
  - Si el controlador ya había iniciado la fase nativa de instalación (`WUA`/`pnputil`), esta no se interrumpe y continúa hasta finalizar su ciclo para garantizar la estabilidad del sistema operativo.
  - En ambos casos, el instalador asienta el resultado correspondiente en el historial (`history.db`).
  - El lote se interrumpe de inmediato: las actualizaciones posteriores de la petición no se inician.
  - Los errores de transporte gRPC/I/O no generan registros espurios ni fallos duplicados en el historial.
  - La UI, al reconectar, puede consultar `GetStatus` (para saber si el servicio sigue ocupado completando el driver) y `GetHistory` (para conocer el resultado final).
- **Cola de operaciones**: `OperationQueue` serializa instalaciones, rollbacks y búsquedas (una a la vez, la sesión COM de WUA no debe atenderlas en paralelo). Al completarse la llamada gRPC o al concluir el proceso de cancelación (respetando la finalización de la fase nativa si ya se inició), la cola queda liberada para la siguiente operación.
- **Creación de instancias del pipe**: hook `CreateNamedPipeServerStream` con `NamedPipeStreamFactory`, que normaliza `FirstPipeInstance` (solo la primera instancia real) y aplica la ACL del contexto.
- **Ajustes** (`GetSettings`/`UpdateSettings`): se añadirán al contrato junto con la gestión de ajustes y exclusiones (Hito 2).

## 6. Flujo de actualización

1. **Escaneo**: se obtiene el inventario de drivers instalados.
2. **Búsqueda**: se consulta la fuente (Windows Update) y se cruzan resultados por hardware ID.
3. **Filtrado**: se descartan los ya actualizados, los excluidos por el usuario y los no firmados.
4. **Selección**: el usuario elige uno, varios o todos.
5. **Pre-instalación**: descarga, verificación de hash y firma, creación de punto de restauración.
6. **Instalación**: una a una, registrando resultado.
7. **Post-instalación**: verificación de que el dispositivo funciona (estado PnP sin error), aviso de reinicio si hace falta.
8. **Fallo**: rollback automático del driver afectado y entrada en el log.

## 7. Datos locales

- Ruta: `%ProgramData%\DriverUpdater\`
  - `config.json`: ajustes (frecuencia de comprobación, exclusiones, solo WHQL).
  - `history.db` (SQLite): operaciones, versión anterior/nueva, resultado, hash.
  - `logs\`: Serilog con rotación.
- Permisos: escritura solo para SYSTEM y Administradores.

## 8. Seguridad

| Amenaza | Mitigación |
|---|---|
| Driver manipulado | Verificación de firma (WinVerifyTrust) y hash antes de instalar |
| Driver que rompe el sistema | Punto de restauración, rollback, lista de bloqueo, instalación una a una |
| Elevación de privilegios vía IPC | Named pipe con ACL restringida; el servicio valida y solo expone operaciones de alto nivel, nunca "ejecutar comando" |
| Inyección de argumentos | `ArgumentList`, validación estricta de rutas e IDs |
| Man-in-the-middle (backend) | TLS, y firma del catálogo con clave propia |
| Falsos positivos de antivirus | Firma de código (certificado EV recomendado), instalador firmado, reputación |
| Autoactualización comprometida | Actualizaciones firmadas y verificadas antes de aplicar |

## 9. Rendimiento

- Servicio en reposo: objetivo < 50 MB de RAM y CPU cercana a 0 %.
- Escaneo completo en menos de 30 s en un equipo típico.
- Evitar polling: comprobaciones programadas, no bucles continuos.

## 10. Pruebas

- Unitarias en `Core` (versiones, matching, selección).
- Integración en `Agent.Drivers` sobre VM (Windows 10 y 11) con snapshots para volver atrás.
- Matriz de hardware mínima: VM, portátil Intel, equipo AMD, equipo con GPU NVIDIA.
- Pruebas de fallo: corte de red en descarga, driver con firma inválida, reinicio durante instalación.

## 11. Distribución

- Instalador MSI (WiX) firmado; registra el servicio y la UI.
- Autoactualización del propio producto (fase 4).
- Canales `stable` y `beta`.

## 12. Cuestiones abiertas

1. **Fuente de drivers más allá de Windows Update**: catálogos OEM (Dell, HP, Lenovo) y fabricantes de componentes; revisar licencias antes de integrar.
2. **Público objetivo**: particulares o empresas; condiciona el backend.
3. **Modelo de negocio**: gratuito, de pago o freemium.
4. **WPF vs WinUI 3**: WPF por madurez; la UI es intercambiable al estar aislada tras gRPC.
5. **Certificado de firma de código**: elegir proveedor y presupuestar.
