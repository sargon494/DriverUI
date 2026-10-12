# AGENTS.md

Guía para agentes de IA (y personas) que trabajen en este repositorio.

## Proyecto

**DriverUpdater**: aplicación para Windows 10/11 que detecta e instala automáticamente actualizaciones de drivers de forma segura (solo drivers firmados, con punto de restauración previo y rollback).

Documentos relacionados:
- `DESIGN.md`: arquitectura y decisiones técnicas.
- `MVP.md`: alcance, tareas y criterios de aceptación de la primera versión.

## Stack

| Capa | Tecnología |
|---|---|
| Lenguaje | C# 14 / .NET 10 (LTS) |
| Servicio de agente | Worker Service (servicio de Windows, cuenta SYSTEM) |
| Acceso a Windows | WMI (`System.Management`), Windows Update Agent (COM), `pnputil`, WinVerifyTrust (P/Invoke) |
| IPC UI ↔ servicio | gRPC sobre named pipes |
| UI | WPF (MVVM con CommunityToolkit.Mvvm) |
| Backend (fase posterior) | ASP.NET Core + EF Core + PostgreSQL |
| Tests | xUnit, FluentAssertions, NSubstitute |
| Instalador | WiX (MSI) |

## Estructura del repositorio

```
DriverUpdater/
├─ src/
│  ├─ Core/            # modelos, versiones, matching de hardware IDs (sin dependencias de Windows)
│  ├─ Agent.Drivers/   # WMI, WUA, pnputil, firmas, restore points
│  ├─ Agent.Service/   # Worker Service y servidor gRPC
│  ├─ Ui.Wpf/          # interfaz de usuario
│  ├─ Cli/             # herramienta de pruebas sin UI
│  └─ Backend.Api/     # (fase posterior)
├─ tests/
├─ installer/
├─ AGENTS.md
├─ DESIGN.md
└─ MVP.md
```

## Comandos

```powershell
dotnet restore
dotnet build -c Release
dotnet test
dotnet run --project src/Cli -- scan        # lista drivers instalados
dotnet run --project src/Cli -- updates     # busca actualizaciones
dotnet format --verify-no-changes           # comprobar formato
```

Las operaciones que instalan drivers requieren consola de administrador y **deben probarse en una máquina virtual**, nunca en el equipo principal de desarrollo.

## Convenciones de código

- Nullable reference types activado; cero warnings en `Release`.
- `async/await` de extremo a extremo; todos los métodos asíncronos aceptan `CancellationToken`.
- Inyección de dependencias con `Microsoft.Extensions.DependencyInjection`; nada de singletons estáticos.
- Todo acceso a Windows (WMI, COM, pnputil, P/Invoke) se esconde detrás de interfaces definidas en `Core` para poder simular en tests.
- Registro con `ILogger` (Serilog como sink). Nunca `Console.WriteLine` fuera de `Cli`.
- Nombres en inglés en el código; documentación y mensajes de commit pueden estar en español.
- Commits pequeños con mensaje en imperativo (`Add driver inventory service`).

## Reglas de seguridad (obligatorias)

1. **Nunca instalar un driver sin verificar su firma digital** (WinVerifyTrust). Por defecto, solo WHQL / firmados por Microsoft.
2. **Siempre crear un punto de restauración** antes de instalar. Si falla, abortar la instalación.
3. La UI **nunca** se ejecuta con privilegios elevados. Todo lo privilegiado pasa por el servicio.
4. El servicio solo acepta conexiones de la UI local (ACL del named pipe restringida a usuarios interactivos).
5. No ejecutar comandos construyendo cadenas con entrada externa; usar `ProcessStartInfo.ArgumentList`.
6. Toda descarga se verifica con hash (SHA-256) y firma antes de ejecutarse.
7. No guardar secretos en el repositorio. Usar `dotnet user-secrets` o variables de entorno.
8. Cada instalación deja registro en el log de operaciones (qué, cuándo, versión anterior, resultado).

## Pruebas

- Lógica pura (`Core`): tests unitarios, sin dependencias de Windows.
- `Agent.Drivers`: tests de integración marcados con `[Trait("Category","Integration")]`, que solo se ejecutan en Windows y en VM.
- Un cambio que toque instalación, rollback o verificación de firmas **requiere test** o una nota explicando cómo se verificó manualmente.

## Qué NO hacer

- No añadir drivers, binarios ni instaladores de terceros al repositorio.
- No desactivar la verificación de firmas, ni siquiera "temporalmente" o detrás de un flag sin documentar.
- No introducir telemetría o llamadas de red sin que esté descrita en `DESIGN.md`.
- No cambiar la arquitectura (por ejemplo, ejecutar la UI como administrador) sin actualizar `DESIGN.md`.
- No ejecutar código de instalación contra el equipo real del desarrollador.

## Cómo trabajar en una tarea

1. Leer `MVP.md` y localizar la tarea y su criterio de aceptación.
2. Cambios mínimos y enfocados; si surge una decisión de diseño, anotarla en `DESIGN.md`.
3. Compilar, ejecutar tests y `dotnet format` antes de dar la tarea por terminada.
4. Resumir qué se hizo, qué se probó y qué queda pendiente.
