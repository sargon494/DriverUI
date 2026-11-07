<div align="center">

# DriverUpdater

**Busca actualizaciones de drivers de Windows y aplica controles de seguridad en Windows 10 y 11**

[![CI](https://github.com/sargon494/DriverUI/actions/workflows/ci.yml/badge.svg)](https://github.com/sargon494/DriverUI/actions/workflows/ci.yml)
[![GitHub Pages](https://github.com/sargon494/DriverUI/actions/workflows/pages.yml/badge.svg)](https://github.com/sargon494/DriverUI/actions/workflows/pages.yml)
[![.NET 10 LTS](https://img.shields.io/badge/.NET-10.0%20LTS-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011%20(64--bit)-0078D6?logo=windows&logoColor=white)](https://microsoft.com/windows)
[![Seguridad WHQL](https://img.shields.io/badge/Firma-WHQL%20%2F%20Microsoft-10b981?logo=shield&logoColor=white)](#-controles-de-seguridad)
[![Licencia MIT](https://img.shields.io/badge/Licencia-MIT-blue.svg)](LICENSE)

<br />

### 🌐 [Probar Simulador Interactivo en GitHub Pages](https://sargon494.github.io/DriverUI/)

</div>

---

## 💡 ¿Por qué DriverUpdater?

**DriverUpdater** es un proyecto comunitario gratuito y de código abierto, pensado como alternativa a las aplicaciones comerciales de actualización de controladores. No desbloquea, modifica ni evita licencias de otros programas: ofrece una aplicación independiente. En esta versión busca actualizaciones mediante Windows Update Agent (WUA); no mantiene un catálogo ni un servidor de descarga propios. La instalación aplica comprobaciones de firma y requiere un punto de restauración previo según la configuración y el estado del sistema.

---

## 🛡️ Controles de seguridad

| Garantía | ¿Cómo te protege? |
|---|---|
| **1. Comprobación de firmas** | El servicio comprueba firmas mediante `WinVerifyTrust`; la política WHQL está activada por defecto, pero puede modificarse en Ajustes. Una firma válida no garantiza que un controlador sea seguro, compatible o libre de defectos. |
| **2. Punto de restauración** | Antes de instalar se solicita un punto de restauración; si no se puede crear, el flujo de instalación se aborta. La disponibilidad y eficacia de Restaurar sistema dependen de Windows y de su configuración. |
| **3. Rollback** | La aplicación ofrece una operación para revertir una instalación registrada. No se puede garantizar que la reversión recupere el estado anterior en todos los equipos. |
| **4. Separación de privilegios** | La UI se ejecuta como usuario estándar y solicita operaciones al servicio de Windows, que tiene privilegios elevados. Esto reduce la exposición de la UI, pero no elimina todos los riesgos. |
| **5. Historial local** | El historial SQLite y los registros se guardan localmente en `%ProgramData%\DriverUpdater`. No se eliminan automáticamente al desinstalar; consulta [Privacidad](PRIVACY.md) para conocer los datos y cómo borrarlos. |

Estos controles reducen algunos riesgos, pero la instalación de controladores puede causar fallos o pérdida de funcionalidad. Haz copias de seguridad y revisa cada actualización antes de instalarla.

---

## 🔎 Datos y conexiones

La aplicación no tiene backend propio, cuentas ni analítica del proyecto. Consulta la [nota de privacidad](PRIVACY.md) para saber qué información se guarda localmente y cómo Windows Update y los sitios de terceros pueden tratar datos.

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
- 🗺️ [`ARCHITECTURE.md`](ARCHITECTURE.md): Arquitectura implementada, flujos y puntos de extensión.
- 📐 [`DESIGN.md`](DESIGN.md): Decisiones técnicas vigentes y cuestiones abiertas.
- 🎯 [`MVP.md`](MVP.md): Alcance, historias de usuario y criterios de aceptación.
- 🧪 [`TESTING.md`](TESTING.md): Procedimientos manuales y registro de resultados en VM.
- 📋 [`SPRINT_LOG.md`](SPRINT_LOG.md): Historial cronológico; no sustituye al estado actual.
- 🛡️ [`AGENTS.md`](AGENTS.md): Convenciones, reglas de seguridad y mapa de documentación.

---

## 📄 Licencia

El código de este proyecto se ofrece bajo la licencia [MIT](LICENSE). Los instaladores públicos pueden incluir dependencias y componentes de terceros con licencias y avisos propios. Antes de publicar cada versión compilada, hay que generar y revisar un inventario/SBOM de dependencias y sus avisos de licencia, incluidos los componentes del runtime que se empaqueten.

## ⚠️ Aviso de uso

El software se proporciona «tal cual», conforme a la licencia MIT. No se garantiza que una actualización sea adecuada para un equipo concreto, ni que un punto de restauración o rollback siempre permita recuperar el estado anterior. El usuario decide qué instalar y asume los riesgos de cambios en controladores. No lo uses en equipos críticos sin validación y copias de seguridad adecuadas.

DriverUpdater es un proyecto comunitario independiente y no está afiliado, patrocinado ni respaldado por Microsoft ni por los fabricantes de dispositivos. Windows, Microsoft y las marcas de terceros pertenecen a sus respectivos titulares.
