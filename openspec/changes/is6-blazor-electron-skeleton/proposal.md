## Why

El change `is1-embedded-postgres-spike` (issue #1) cerró la pregunta de la base de datos: PostgreSQL en los dos modos, embebido en el modo autónomo. Falta la otra mitad de la prueba de concepto: **la propia aplicación**. Vagalume quiere un único código que funcione como **aplicación de escritorio autónoma** y como **servicio web alojado por una empresa** al que se accede desde el navegador.

La exploración previa del proyecto propone la solución: **Blazor Server** como interfaz compartida (sin API REST entre interfaz y lógica, porque los componentes se ejecutan en el servidor), **Electron.NET** como contenedor de escritorio y una estructura por capas que permite cambiar de contenedor sin reescribir la interfaz. Hasta ahora eso es una inclinación, no una decisión, y ningún dato la respalda.

Este change **cierra la decisión de usar Blazor Server y la arquitectura de código** con un esqueleto ejecutable de punta a punta, y mide con evidencia lo que la nota solo supone: si Electron.NET Core (una versión 0.x) arranca y detiene bien el servidor local, si el puerto local puede protegerse de otros procesos de la máquina y si se puede empaquetar un instalador para Linux x64 y Windows x64 que lleve dentro el PostgreSQL embebido.

Es un **prototipo exploratorio**: no construye ninguna parte de un dominio de facturación. Su resultado es un esqueleto que sirve de base y un informe de hallazgos.

## What Changes

**Solución por capas**

- Cinco proyectos nuevos junto a la librería de PostgreSQL embebido existente: `Vagalume.Core` (dominio y casos de uso, .NET puro), `Vagalume.Data` (persistencia con EF Core), `Vagalume.UI` (Razor Class Library con componentes y páginas), `Vagalume.Host.Web` (ASP.NET Core desplegable en un servidor) y `Vagalume.Host.Desktop` (el mismo ASP.NET Core dentro de Electron.NET).
- Reglas de dependencia comprobadas por una prueba automática: `Core` no depende de nada; `Data` y `UI` dependen solo de `Core`; los hosts son la raíz de composición y los únicos que conocen todo lo demás; solo `Host.Desktop` conoce Electron.NET y el PostgreSQL embebido.

**Interfaz Blazor Server compartida**

- Blazor Web App con modo de renderizado interactivo de servidor únicamente (sin WebAssembly), con la interfaz en `Vagalume.UI`. Los componentes inyectan servicios de `Core` directamente; no hay API REST.

**Persistencia con EF Core sobre PostgreSQL**

- EF Core con el proveedor de Npgsql como única opción de persistencia, una única cadena de migraciones y un modelo mínimo (notas de texto) que demuestra el recorrido interfaz → `Core` → `Data` → PostgreSQL, incluido el token de concurrencia optimista `xmin` propio de PostgreSQL.

**Host web**

- Lee la cadena de conexión de la configuración (un PostgreSQL externo, por ejemplo en un contenedor), falla con un error claro si falta, aplica las migraciones al arrancar y sirve la interfaz.

**Host de escritorio**

- Arranca el PostgreSQL embebido de `Vagalume.Postgres.Embedded` en el perfil del usuario, aplica las migraciones, sirve la interfaz en un puerto local solo en `127.0.0.1` y la muestra en una ventana de Electron.NET Core. Al cerrar la ventana, detiene el servidor de base de datos.
- **Seguridad local**: un token aleatorio generado en cada arranque protege el puerto local; sin él, ningún otro proceso de la máquina puede usar la aplicación.
- **Empaquetado**: paquetes ejecutables para Linux x64 y Windows x64 que incluyen solo los binarios de PostgreSQL de su propia plataforma.

**Informe de hallazgos**

- Un apartado del `README.md` con lo que el esqueleto ha demostrado y lo que no: estabilidad de Electron.NET Core, ciclo de vida, tamaño y tiempos del paquete, resultado de la seguridad local y lo que no se ha podido validar (Windows).

## Capabilities

### New Capabilities

- `solution-architecture`: capas de la solución y reglas de dependencia entre proyectos, comprobables automáticamente.
- `sample-notes`: modelo mínimo de notas que demuestra el recorrido de extremo a extremo (interfaz, casos de uso, persistencia y concurrencia optimista) sobre PostgreSQL.
- `web-host`: host ASP.NET Core desplegable en un servidor, con PostgreSQL externo por configuración.
- `desktop-host`: host de escritorio con Electron.NET Core, PostgreSQL embebido, protección del puerto local por token y empaquetado para Linux x64 y Windows x64.

### Modified Capabilities

Ninguna. La capability `embedded-postgres-host` se consume tal cual, sin cambiar sus requisitos.

## Impact

- **Repositorio**: nuevos proyectos bajo `src/` (`Vagalume.Core`, `Vagalume.Data`, `Vagalume.UI`, `Vagalume.Host.Web`, `Vagalume.Host.Desktop`) y bajo `tests/`, incluido un proyecto de apoyo con el entorno de pruebas de PostgreSQL que hoy vive dentro de `Vagalume.Postgres.Embedded.Tests`. Manifiesto de herramientas local (`dotnet-ef`). Se amplía la configuración de `.gitignore` para los resultados de empaquetado. Se actualizan `README.md` y `AGENTS.md`.
- **Dependencias NuGet nuevas (aprobadas por este change)**: `ElectronNET.Core` y `ElectronNET.Core.AspNet` (Host.Desktop), `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design` y `Npgsql.EntityFrameworkCore.PostgreSQL` (Data), `Microsoft.AspNetCore.Mvc.Testing` (pruebas de los hosts).
- **Herramientas de build nuevas**: Node.js 22 o posterior y las dependencias de npm que traiga Electron.NET Core (Electron y el empaquetador), solo en tiempo de build. La aplicación en ejecución no descarga nada.
- **CI**: sin cambios; sigue sin existir (issue #3).
- **Fuera de alcance**: ver *Non-Goals* en `design.md`.
