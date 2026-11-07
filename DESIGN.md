# Decisiones técnicas

Este documento registra decisiones transversales y sus motivos; no intenta duplicar el mapa de componentes ni el historial de implementación.

- La arquitectura actual y sus flujos están en [`ARCHITECTURE.md`](ARCHITECTURE.md).
- El alcance y el estado de aceptación están en [`MVP.md`](MVP.md).
- Las pruebas y su evidencia están en [`TESTING.md`](TESTING.md).
- Los detalles cronológicos de cada sprint están en [`SPRINT_LOG.md`](SPRINT_LOG.md). Las notas históricas no son especificaciones actuales.

## Decisiones vigentes

### D1. C#/.NET para la aplicación

Se eligió C#/.NET para compartir lenguaje entre el servicio Windows, la UI y posibles componentes futuros, y para usar directamente las API de Windows. Un backend futuro no forma parte del MVP y no debe condicionar el funcionamiento local.

### D2. Separación entre UI y servicio privilegiado

La UI WPF se ejecuta como usuario estándar. El servicio Windows concentra las operaciones que modifican el sistema; la comunicación local usa gRPC sobre un named pipe con ACL. No se elevará la UI para simplificar flujos. La arquitectura implementada está en `ARCHITECTURE.md`; los procedimientos de validación están en `TESTING.md`.

### D3. Windows Update como fuente del MVP

El MVP consulta Windows Update Agent. No se redistribuyen paquetes desde un catálogo propio. Los enlaces OEM son una ayuda que el usuario abre en el navegador; no implican descarga ni instalación desde esos fabricantes.

### D4. Política de instalación

Se exige una firma válida y se aplica WHQL por defecto (`OnlyWhql = true`); el ajuste permite desactivar únicamente el requisito WHQL, no la comprobación de validez de firma. La instalación requiere un punto de restauración previo. Estas son invariantes de diseño, no una afirmación de que cada camino de ejecución las aplique correctamente: `ARCHITECTURE.md` registra la limitación observada en el adaptador WUA hasta que se resuelva.

### D5. Comprobaciones periódicas sin notificaciones de escritorio

La comprobación configurable (desactivada, diaria o semanal) se ejecuta mientras el servicio está activo. Su resultado se comunica mediante el indicador de Actualizaciones dentro de la aplicación. No se muestran notificaciones toast ni se crea una tarea programada de usuario.

## Decisiones pendientes

- Evaluar fuentes de drivers adicionales solo después de revisar licencias, procedencia y un mecanismo de manifiesto/hash/firma.
- Definir si el producto seguirá dirigido a usuarios particulares o también a empresas antes de diseñar cuentas, licencias o backend.
- Decidir el modelo de negocio y si existe una necesidad real de migrar de WPF.
- Elegir un certificado de firma de código antes de distribuir binarios firmados.

## Cómo actualizar este registro

Añade aquí decisiones nuevas que afecten a límites de confianza, compatibilidad, dependencias compartidas o comportamiento del producto. Actualiza `ARCHITECTURE.md` cuando cambie la implementación y `MVP.md` cuando cambien requisitos o criterios. El `SPRINT_LOG.md` conserva el contexto histórico; no copies allí el estado vigente como sustituto de estos documentos.
