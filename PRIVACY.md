# Privacidad y tratamiento de datos

Última actualización: 7 de noviembre de 2026

Esta nota describe el comportamiento conocido de la versión comunitaria de
DriverUpdater en este repositorio. No es asesoramiento jurídico ni sustituye
la información de privacidad de los servicios de terceros que se mencionan.

## Aplicación de Windows

La aplicación no tiene cuentas, backend propio ni un servicio de analítica o
telemetría del proyecto. El proyecto no recibe los datos que la aplicación lee
del equipo.

Para funcionar, la aplicación puede acceder localmente a:

- Inventario de dispositivos y controladores (incluidos nombres, fabricante,
  versiones, identificadores de dispositivo y hardware).
- Ajustes y exclusiones de dispositivos que el usuario guarda.
- Historial de operaciones: dispositivo, identificador de hardware, título de
  actualización, versiones, fecha, resultado y, si ocurre, mensaje de error.
- Registros técnicos del servicio, que pueden incluir detalles de errores.

La configuración, el historial y los registros se guardan en el equipo, bajo
`%ProgramData%\DriverUpdater`. La aplicación no los envía al mantenedor. La
desinstalación actual no elimina esta carpeta. Para borrar esos datos, cierra
la aplicación, desinstálala y elimina manualmente la carpeta. La herramienta
CLI también puede crear registros en una carpeta `logs` relativa al directorio
desde el que se ejecuta.

Al buscar o instalar controladores, la aplicación utiliza Windows Update Agent
(WUA) de Windows. La comunicación con Windows Update la realizan Windows y sus
componentes; puede implicar información técnica necesaria para buscar,
descargar o instalar actualizaciones. El proyecto no controla ni puede
describir de forma exhaustiva los datos que Windows transmite, sus periodos de
conservación o sus destinatarios. Consulta la documentación y la configuración
de privacidad de Microsoft y Windows Update. Si se activa la comprobación
periódica, el servicio puede iniciar estas búsquedas según el intervalo
configurado mientras esté ejecutándose.

La aplicación también puede abrir en el navegador, a petición del usuario,
páginas de soporte de fabricantes. Esos sitios se rigen por sus propias
políticas.

## Sitio web del proyecto

La página de demostración se sirve mediante GitHub Pages. El sitio no incluye
un servicio de analítica del proyecto y las fuentes tipográficas se sirven
desde el propio sitio; GitHub puede tratar datos de conexión y uso conforme a
sus propias condiciones y política de privacidad. Los enlaces a GitHub,
Microsoft y otros sitios llevan a servicios de terceros.

## Elección y control

El inventario y los datos guardados se mantienen en el equipo. Puedes dejar de
usar la aplicación y borrar los datos locales siguiendo los pasos anteriores.
No publiques identificadores de hardware, registros o datos de tu equipo en
incidencias o foros públicos.

## Responsable y consultas

Este es un proyecto comunitario gratuito publicado por Sargon494. El código y
los instaladores pueden estar disponibles públicamente en GitHub. No se ha
indicado una entidad jurídica ni un canal privado de contacto para solicitudes
formales de privacidad. Para consultas generales sobre el código, utiliza los
canales del repositorio de GitHub y no incluyas información personal o
identificadores de hardware en una incidencia pública.

Antes de ofrecer los instaladores públicamente, el mantenedor debe confirmar
los datos de contacto adecuados, revisar los requisitos legales aplicables en
los territorios a los que se dirige el proyecto y comprobar esta nota con
asesoramiento cualificado si corresponde. Esta nota no afirma que cumpla por
sí sola con todas las obligaciones legales.
