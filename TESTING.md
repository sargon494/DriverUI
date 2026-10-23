# Plan de pruebas en VM

Pruebas manuales que no pueden hacerse en CI: instalación real, UI con cuenta estándar, fallos a mitad de operación y reinicios. Las pruebas automáticas (tests de `tests/`) se ejecutan en el CI; este documento cubre lo que queda.

Referencias: `installer/README.md` (compilar e instalar), `ARCHITECTURE.md` (qué hace cada pieza), `MVP.md` (Hito 3).

## 0. Preparación (una vez por VM)

- VM con Windows 10 (22H2) y otra con Windows 11 (24H2). Sin datos personales.
- Restauración del sistema activa, o la instalación fallará al crear el punto de restauración:
  ```powershell
  Enable-ComputerRestore -Drive "C:\"
  ```
- Dos cuentas: una **administradora** para instalar y una **estándar** para usar la UI.
- Guardar un **checkpoint "limpio"** tras instalar el MSI y antes de cada escenario. Restaurarlo entre pruebas.
- Ejecutar el MSI con registro:
  ```powershell
  msiexec /i installer\artifacts\DriverUpdater-X.Y.Z-x64.msi /l*v install.log
  ```

## 1. Qué no hacer

- **No desinstales drivers de red, almacenamiento, pantalla ni chipset en la VM.** Si pierdes la red o el disco, no podrás leer los logs ni restaurar. Si quieres ver actualizaciones, primero comprueba si Windows Update ofrece alguna (la propia app lo muestra). En una VM suele haber pocas o ninguna, y eso ya es un resultado: anótalo.
- Nunca ejecutes estas pruebas en el equipo de desarrollo (`AGENTS.md`).
- Si quieres un driver "con actualización" para probar la instalación, prepáralo en la VM con un checkpoint y un dispositivo no crítico, nunca sobre un dispositivo que la VM necesite para arrancar.

## 2. Escenarios

Para cada uno: anota la versión del MSI, el checkpoint usado y el resultado esperado frente al observado. Adjunta `install.log` y los `logs\*.log` de `%ProgramData%\DriverUpdater\`.

### 2.1 Arranque y conexión (cuenta estándar)

1. Instalar con la cuenta administradora. Cerrar sesión.
2. Entrar con la cuenta estándar y abrir DriverUpdater.
3. **Esperado:** "Conectando con el servicio" durante unos segundos y después "Servicio activo". No aparece ningún diálogo de error.
4. Reiniciar la VM. **Esperado:** el servicio **no** está en marcha (`sc query DriverUpdater` → `STOPPED`). Abrir la UI lo arranca.

### 2.2 Corte de red durante la búsqueda

1. Abrir Actualizaciones y pulsar "Buscar".
2. Mientras busca, desactivar el adaptador: `Disable-NetAdapter -Name "Ethernet" -Confirm:$false`.
3. **Esperado:** la búsqueda termina con un mensaje de error legible. La UI no se cierra ni muestra excepciones. El servicio sigue respondiendo a `GetStatus` (la UI sigue conectada).
4. Reactivar el adaptador y buscar de nuevo. **Esperado:** la búsqueda funciona.

### 2.3 Firma inválida o ausente

No se puede probar con un driver real sin riesgo. La prueba automática es la de referencia: un `ISignatureVerifier` de prueba que rechaza la firma debe impedir la instalación y registrar el rechazo. Comprobar en la VM solo que, cuando la instalación real se rechaza, la app muestra el motivo y el historial lo recoge.

- **Esperado:** no se instala nada, no se modifica el sistema y el historial registra la operación como fallida con el motivo.

### 2.4 Reinicio durante la instalación

1. Crear el checkpoint, elegir una actualización y pulsar "Instalar".
2. Cuando la barra avance (fase de instalación), reiniciar la VM **desde el host** (`Restart-VM -Force`), no desde dentro.
3. Al arrancar, abrir la UI con la cuenta estándar.
4. **Esperado:**
   - La UI conecta y no hay operación en curso (la cola se reinicia vacía).
   - El historial muestra la operación como interrumpida o fallida, con su punto de restauración.
   - Si Windows pide reinicio, aparece el aviso "Reinicio pendiente".
5. Anotar qué drivers quedaron instalados y comprobar que el dispositivo funciona (Administrador de dispositivos, sin signo de exclamación).

### 2.5 Reinicio pendiente leído del registro

Prueba de la última funcionalidad (`GetStatus` lee las marcas de Windows). No requiere ninguna actualización.

1. Como administrador, crear la marca que Windows deja al pedir reinicio:
   ```powershell
   New-Item -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending" -Force
   ```
2. Reiniciar el servicio: `Restart-Service DriverUpdater`. Abrir la UI.
3. **Esperado:** aparece "Reinicio pendiente" aunque el servicio acabe de arrancar.
4. Quitar la marca:
   ```powershell
   Remove-Item -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"
   ```
   Reiniciar el servicio otra vez. **Esperado:** el aviso desaparece.

### 2.6 Exclusión (U7)

1. Con una actualización en la lista, pulsar "Excluir".
2. **Esperado:** desaparece de la lista. En Ajustes aparece su hardware ID.
3. Pulsar "Buscar" de nuevo. **Esperado:** no vuelve a aparecer.
4. Quitar la exclusión en Ajustes, guardar y buscar. **Esperado:** vuelve a aparecer.

### 2.7 Desinstalación

1. `msiexec /x` con el MSI original (o desde Programas).
2. **Esperado:** el servicio se detiene y se elimina (`sc query DriverUpdater` → no existe), no quedan ficheros en `Program Files\DriverUpdater`, y `%ProgramData%\DriverUpdater` se conserva (decisión pendiente, ver `installer/README.md`).

## 3. Registro de resultados

Cada ejecución se anota aquí o en el Hito 3 de `MVP.md`:

| Escenario | Windows | MSI | Resultado | Notas |
|---|---|---|---|---|
| 2.1 Arranque | | | | |
| 2.2 Corte de red | | | | |
| 2.3 Firma | | | | |
| 2.4 Reinicio en instalación | | | | |
| 2.5 Reinicio pendiente | | | | |
| 2.6 Exclusión | | | | |
| 2.7 Desinstalación | | | | |
