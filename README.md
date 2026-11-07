<div align="center">

# DriverUpdater

**Actualizaciones de drivers oficiales, verificadas y 100% seguras para Windows 10 y 11**

[![CI](https://github.com/sargon494/DriverUI/actions/workflows/ci.yml/badge.svg)](https://github.com/sargon494/DriverUI/actions/workflows/ci.yml)
[![GitHub Pages](https://github.com/sargon494/DriverUI/actions/workflows/pages.yml/badge.svg)](https://github.com/sargon494/DriverUI/actions/workflows/pages.yml)
[![.NET 10 LTS](https://img.shields.io/badge/.NET-10.0%20LTS-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011%20(64--bit)-0078D6?logo=windows&logoColor=white)](https://microsoft.com/windows)
[![Seguridad WHQL](https://img.shields.io/badge/Firma-WHQL%20%2F%20Microsoft-10b981?logo=shield&logoColor=white)](#-las-5-garantías-de-seguridad)
[![Licencia MIT](https://img.shields.io/badge/Licencia-MIT-blue.svg)](LICENSE)

<br />

### 🌐 [Probar Simulador Interactivo en GitHub Pages](https://sargon494.github.io/DriverUI/)

</div>

---

## 💡 ¿Por qué DriverUpdater?

Muchos programas conocidos para "actualizar controladores" son poco fiables: instalan publicidad no deseada, intentan cobrar suscripciones, descargan drivers de servidores web no oficiales o instalan versiones beta que acaban provocando pantallazos azules (**BSOD**).

**DriverUpdater** se creó con una filosofía totalmente opuesta:
- **Sin publicidad ni costes**: Código abierto (Open Source), gratuito y transparente.
- **Solo fuentes 100% oficiales**: Se conecta directamente al catálogo oficial de **Windows Update** (WUA) de Microsoft. No existen servidores intermediarios ni repositorios de terceros.
- **Seguridad primero**: Si un driver no tiene una firma digital válida o si no se puede crear un punto de restauración en Windows, **la instalación se detiene automáticamente**.

---

## 🛡️ Las 5 Garantías de Seguridad

| Garantía | ¿Cómo te protege? |
|---|---|
| **1. Solo controladores certificados (WHQL)** | Antes de instalar un archivo `.inf` o `.sys`, se comprueba mediante la API de Windows `WinVerifyTrust` que su firma digital pertenece a Microsoft y cumple con los identificadores de certificación de hardware **WHQL**. |
| **2. Punto de restauración automático** | Antes de instalar cualquier actualización, se genera un punto de restauración en Windows. Si algo falla o el sistema no permite crearlo, **no se modifica absolutamente nada de tu equipo**. |
| **3. Deshacer cambios en 1 clic (*Rollback*)** | ¿Un controlador nuevo da problemas de sonido o imagen? En la pestaña de Historial puedes pulsar **Revertir** para volver al controlador anterior que funcionaba bien. |
| **4. Tu usuario sin riesgos (Sin UAC continuo)** | La interfaz gráfica se ejecuta con tu usuario normal, sin privilegios de administrador. Las tareas delicadas las realiza un servicio en segundo plano de Windows de forma aislada. |
| **5. Historial permanente y transparente** | Todas las operaciones quedan registradas en una base de datos local SQLite (`history.db` en `%ProgramData%\DriverUpdater\`), para que siempre sepas qué versión tenías y cuál se aplicó. |

---

## 🖥️ ¿Cómo funciona la aplicación?

La aplicación está diseñada para ser intuitiva y clara para cualquier persona:

```
┌────────────────────────────────────────────────────────────────────────┐
│  DriverUpdater — Panel de Control                                      │
├───────────────┬────────────────────────────────────────────────────────┤
│ 🏠 Inicio     │  Estado de salud: [ 185 Dispositivos Operativos  ✓ ]   │
│ 🔄 Actualizar │                                                        │
│ 📜 Historial  │  Placa base: Gigabyte B760M DS3H DDR4                  │
│ ⚙️ Ajustes    │  [ Enlace a soporte oficial de controladores del OEM ] │
└───────────────┴────────────────────────────────────────────────────────┘
```

### 1. Pantalla de Inicio (Dashboard)
- Muestra el estado global de los componentes de tu equipo (tarjeta gráfica, red, sonido, chipset, almacenamiento).
- Detecta el fabricante y modelo de tu placa base (ASUS, Gigabyte, MSI, ASRock) y te proporciona un botón seguro para abrir la **página oficial de soporte del fabricante** en tu navegador.

### 2. Pantalla de Actualizaciones
- Al pulsar **Buscar actualizaciones**, consulta a Windows Update qué drivers oficiales tienen una versión más reciente para tu hardware exacto.
- Te muestra una comparativa clara: **Versión actual instalada → Nueva versión recomendada** y el tamaño de la descarga.
- Puedes marcar qué controladores quieres actualizar y pulsar **Instalar seleccionadas**. Verás una barra de progreso en tiempo real con las fases (`Descargando` → `Verificando firma` → `Creando punto de restauración` → `Instalando`).
- Si un componente no quieres actualizarlo nunca (por ejemplo, una tarjeta de sonido dedicada o un mando especial), dispones de un botón **Excluir**.

### 3. Pantalla de Historial
- Muestra una auditoría detallada de cada driver instalado o revertido: fecha, dispositivo, versión previa y resultado.
- Si una actualización te da problemas, seleccionas la fila y pulsas **Revertir controlador** para volver inmediatamente a la versión previa.

### 4. Pantalla de Ajustes
- **Comprobación automática**: Elige si el servicio en segundo plano debe comprobar si hay drivers nuevos (desactivado, cada 24 horas o cada semana).
- **Directiva WHQL**: Activada por defecto para exigir certificados oficiales de Microsoft.
- **Lista de exclusiones**: Consulta o elimina los identificadores de hardware que hayas decidido no actualizar.

---

## 📥 Instalación

### Requisitos del sistema
- **Windows 10 (versión 22H2 o superior)** o **Windows 11** (64 bits).
- Protección del sistema activa en la unidad del sistema (`C:\`) para permitir puntos de restauración.

### Descarga del instalador
1. Ve a la sección de [**Releases en GitHub**](https://github.com/sargon494/DriverUI/releases).
2. Descarga el archivo de instalación `.msi` (ejemplo: `DriverUpdater-1.0.0-x64.msi`).
3. Ejecuta el instalador con permisos de administrador una sola vez. Registrará el servicio seguro de Windows y creará el acceso directo en tu menú de inicio.
4. Abre **DriverUpdater** desde el Menú Inicio.

---

## ⌨️ Herramienta por línea de comandos (CLI)

Para técnicos de soporte, administradores de sistemas o usuarios avanzados que prefieren la consola sin abrir la interfaz gráfica:

```powershell
# 1. Escanear e inventariar todos los drivers instalados con fabricante y versión
dotnet run --project src/Cli -- scan

# 2. Comprobar actualizaciones oficiales en Windows Update
dotnet run --project src/Cli -- updates

# 3. Ver el registro de auditoría e instalaciones previas
dotnet run --project src/Cli -- log

# 4. Instalar un controlador concreto mediante su identificador
dotnet run --project src/Cli -- install <UpdateId>

# 5. Revertir una operación por su ID de historial
dotnet run --project src/Cli -- rollback <OperationId>
```

---

## ❓ Preguntas Frecuentes (FAQ)

#### ¿Por qué DriverUpdater encuentra menos actualizaciones que otros programas comerciales?
Porque DriverUpdater **solo te ofrece controladores oficiales publicados y certificados por Microsoft** para tu modelo exacto de hardware. Otros programas comerciales indexan versiones beta de foros, drivers de otros ordenadores parecidos o archivos no certificados que ponen en riesgo la estabilidad del equipo.

#### ¿El programa consume recursos en segundo plano?
Casi cero. El servicio en reposo ocupa menos de **50 MB de memoria RAM** y prácticamente **0% de uso de CPU**. No hace escaneos continuos en bucle ni muestra molestas ventanas emergentes o notificaciones de publicidad.

#### ¿Qué ocurre si se va la luz o se cancela una instalación a mitad?
DriverUpdater sigue una política de cancelación segura:
- Si se cancela durante la descarga o verificación, se cancela inmediatamente.
- Si el driver ya ha empezado a instalarse a bajo nivel en Windows, espera a que termine de copiarse dicho archivo de forma atómica para no dejar el subsistema de Windows corrupto.
- Además, siempre tienes el **Punto de Restauración del Sistema** generado previamente para volver atrás.

---

## 🛠️ Para Desarrolladores

### Stack Tecnológico
- **Lenguaje**: C# 14 / .NET 10 (LTS).
- **Servicio Windows**: Worker Service en cuenta SYSTEM con Kestrel y transporte nativo de named pipes.
- **IPC (Comunicación entre procesos)**: gRPC sobre Named Pipe local (`\\.\pipe\DriverUpdater`) con control de acceso estricto a usuarios interactivos (`S-1-5-4`).
- **Acceso a Windows**: WMI (`System.Management`), COM (`Microsoft.Update.Session`), `WinVerifyTrust` nativo (P/Invoke) y `pnputil`.
- **Interfaz de Usuario**: WPF en arquitectura MVVM limpia con `CommunityToolkit.Mvvm`.
- **Pruebas**: 151 pruebas automáticas en xUnit, FluentAssertions y NSubstitute.
- **Instalador**: WiX Toolset v4 (MSI).

### Compilar y probar localmente
```powershell
# Clonar el repositorio
git clone https://github.com/sargon494/DriverUI.git
cd DriverUI

# Restaurar paquetes y compilar en modo Release
dotnet restore
dotnet build -c Release

# Ejecutar la suite completa de 151 pruebas unitarias e integración
dotnet test -c Release

# Generar el instalador MSI con WiX v4
powershell ./installer/build.ps1 -Version 1.0.0
```

### Documentación técnica adicional
- 🗺️ [`ARCHITECTURE.md`](ARCHITECTURE.md): Diagramas de flujo de datos, seguridad IPC y estructura de proyectos.
- 📐 [`DESIGN.md`](DESIGN.md): Decisiones técnicas y registro de especificaciones de diseño.
- 🎯 [`MVP.md`](MVP.md): Alcance del producto, historias de usuario y criterios de aceptación.
- 🧪 [`TESTING.md`](TESTING.md): Guía de pruebas manuales y matriz de escenarios de fallo en máquina virtual (VM).
- 📋 [`SPRINT_LOG.md`](SPRINT_LOG.md): Registro cronológico detallado de todos los sprints.

---

## 📄 Licencia

Este proyecto está bajo la licencia [MIT](LICENSE). Eres libre de usarlo, estudiarlo y mejorarlo.
