## Why

Vagalume es una prueba de concepto que nace del producto **Arume** (facturación y contabilidad, hoy escrito en Java) y que puede acabar desligándose de él. Explora si una misma aplicación .NET puede ejecutarse en **dos modos con el mismo código**: como **aplicación de escritorio autónoma** para un particular o un autónomo, y como **servicio web alojado por la propia empresa** al que acceden varios usuarios desde el navegador.

Ese doble modo plantea una pregunta de base de datos. En el servidor de empresa lo natural es PostgreSQL. En el modo autónomo, mantener un segundo motor distinto (por ejemplo SQLite) obliga a conservar dos dialectos, dos juegos de migraciones y pruebas contra ambos, con diferencias que el ORM no esconde del todo (tipos decimales, concurrencia, colaciones). La alternativa es usar **PostgreSQL en los dos modos**: en el servidor, como cualquier instalación; en el modo autónomo, **empaquetado dentro de la propia aplicación** y lanzado como proceso hijo en el puerto local. Así hay un único motor, un único dialecto y una única cadena de migraciones.

La idea es viable porque existen binarios mínimos de PostgreSQL redistribuibles, publicados por el proyecto Zonky en Maven Central y pensados justo para esto, con licencia PostgreSQL.

Pero «PostgreSQL embebido» no es una librería como SQLite: es un **servidor real con su ciclo de vida**. Antes de apoyar ninguna decisión de producto en esta idea hay que comprobar en la práctica los riesgos que no se ven en el papel: qué contiene realmente el paquete mínimo, si se recupera de un cierre brusco, si el acceso queda restringido de verdad al propio usuario y si funciona igual en Linux y en Windows.

Este change es un **prototipo exploratorio** (spike) que responde a esas preguntas con código y pruebas. **No decide** adoptar esta tecnología ni migrar ningún producto: su resultado es un informe de hallazgos sobre el que se podrá decidir en un change posterior.

## What Changes

**Solución .NET 10 en la raíz del repositorio**

- Una solución con una librería (`Vagalume.Postgres.Embedded`) que gestiona un PostgreSQL embebido y un proyecto de pruebas de integración, con el SDK de .NET fijado por `global.json` y la versión del programa en `Directory.Build.props`.

**Adquisición reproducible y verificada de los binarios**

- Los binarios de PostgreSQL para Linux x64 y Windows x64 se descargan **en tiempo de build** (nunca en ejecución) desde Maven Central, con versión fijada y huella SHA-256 verificada contra un fichero de bloqueo versionado. Cada distribución lleva solo los binarios de su plataforma. El script de descarga es una aplicación de fichero único de .NET para que funcione igual en Linux y en Windows.

**Host de PostgreSQL embebido**

- Inicialización del clúster en el primer arranque (`initdb`), arranque, parada limpia y comprobación de estado mediante `pg_ctl`, con directorio de datos en el perfil del usuario.
- Acceso restringido: escucha solo en `127.0.0.1`, sin socket Unix (para que Linux y Windows se comporten igual), puerto libre elegido al arrancar y autenticación `scram-sha-256` con contraseña generada aleatoriamente, sin modo `trust`.
- Recuperación tras cierre brusco y reutilización de una instancia ya en marcha, sin lanzar un segundo servidor sobre los mismos datos.
- Copia de seguridad y restauración a nivel de ficheros (servidor parado y copia del directorio de datos), porque el paquete mínimo no incluye `pg_dump` ni `pg_restore`.

**Pruebas de integración de los riesgos principales**

- Ciclo completo arrancar → conectar con Npgsql → escribir → parar; muerte forzada del proceso y reinicio con los datos confirmados intactos; copia y restauración en un directorio nuevo; rechazo de conexiones sin contraseña; verificación de la huella de los binarios.

**Informe de hallazgos**

- Un apartado del `README.md` que recoge lo que el prototipo ha demostrado y lo que no: contenido real del paquete mínimo, colaciones disponibles, dependencias de runtime en Windows, tamaño, tiempos de arranque y recomendaciones.

## Capabilities

### New Capabilities

- `embedded-postgres-host`: gestión del ciclo de vida de un servidor PostgreSQL empaquetado con la aplicación (binarios verificados, inicialización, arranque restringido a localhost con autenticación, parada, recuperación, copia de seguridad y restauración) para Linux x64 y Windows x64.

### Modified Capabilities

Ninguna. El repositorio parte vacío.

## Impact

- **Repositorio**: se crea la solución (`Vagalume.slnx`, `src/`, `tests/`), `global.json`, `Directory.Build.props`, `.editorconfig`, la carpeta `eng/` con el script y el fichero de bloqueo de binarios, y el informe de hallazgos en el `README.md`. Se amplía el `.gitignore` con `bin/`, `obj/` y la carpeta de binarios descargados.
- **CI**: sin cambios. No existe workflow todavía; se definirá más adelante, y con él la validación automática en Windows.
- **Documentación**: se actualiza `AGENTS.md` (estructura, comandos y estado) cuando el change se archive.
- **Dependencias externas**: descarga de binarios de Zonky desde Maven Central (solo en build), paquete NuGet Npgsql y librería de pruebas xUnit.
- **Fuera de alcance**: ver *Non-Goals* en `design.md`.
