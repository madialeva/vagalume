## Why

El change `is6-blazor-electron-skeleton` (issue #6) cerró la arquitectura de capas y demostró un esqueleto con **Blazor Server**: la interfaz se ejecuta en el servidor y el navegador solo mantiene una conexión SignalR. Funciona, pero tiene dos inconvenientes que el autor quiere evitar: la interfaz se escribe en **sintaxis Razor** (mezcla de HTML y C# que no le gusta) y la lógica de la interfaz vive en el servidor, de modo que el modo web depende de una conexión permanente y cada sesión ocupa memoria en el servidor.

La alternativa que se quiere probar es **Blazor WebAssembly**: la interfaz se ejecuta en el navegador (o en la ventana de Electron) como código C#, habla con el servidor **solo mediante una API REST** y nunca ve la base de datos. La lógica de servicio queda limpia en el servidor. Para librarse de Razor, los componentes se escriben como **clases C# normales** con un pequeño DSL propio que declara el HTML sin sintaxis Razor.

En modo web esto se sabe viable. La incógnita es el **modo escritorio**: un proceso .NET local con acceso total al sistema, con el runtime de .NET *además* dentro del renderizador de Chromium, hablando por una API REST con un servidor que está a su lado. La nota de exploración del proyecto ya señalaba que WebAssembly encaja mal en escritorio. Este change **comprueba con evidencia** si funciona y a qué coste (tamaño, arranque, complejidad) frente a Blazor Server, sin sustituir el esqueleto actual: ambos conviven para poder compararlos.

Es un **prototipo exploratorio**: reutiliza el modelo de notas y no construye ningún dominio de facturación. Su resultado es un esqueleto alternativo y un informe comparativo.

## What Changes

**Contratos y API REST**

- `Vagalume.Api.Contracts`: DTOs y rutas compartidas entre servidor y cliente. No depende de nada ni conoce `Core`, EF Core o Npgsql.
- `Vagalume.Api`: biblioteca con los *minimal APIs* de notas (listar, crear, modificar) sobre los servicios de `Core`. Traduce los errores de dominio a respuestas HTTP estándar (validación 400, no encontrada 404, conflicto de concurrencia 409) con `ProblemDetails`. El cliente recibe la versión de concurrencia como un valor opaco.
- `Vagalume.Api.Client`: cliente HTTP tipado (`HttpClient`) que implementa el contrato; es lo único que el WASM usa para hablar con el servidor.

**Interfaz WebAssembly sin Razor**

- `Vagalume.Wasm.UI`: proyecto Blazor WebAssembly cuyos componentes son clases C# y no contienen ficheros `.razor`. Incluye un DSL propio mínimo sobre `RenderTreeBuilder` para declarar elementos, atributos, eventos y enlaces de datos. Solo depende de `Api.Contracts` y `Api.Client`.
- La misma página de notas que en Blazor Server (alta con validación, lista, edición con mensaje de conflicto) para poder comparar.

**Host de escritorio WASM**

- `Vagalume.Host.Wasm.Desktop`: ASP.NET Core que sirve los ficheros estáticos del WASM y la API REST en `127.0.0.1`, arranca el PostgreSQL embebido, y se muestra en una ventana de Electron.NET Core. Reutiliza la protección por token de sesión del puerto local, que ahora cubre también las llamadas de la API.
- Paquete ejecutable para Linux x64 con los binarios de PostgreSQL de su plataforma.
- Para no duplicar código sensible, las piezas comunes de los hosts de escritorio (token de sesión, guardia del puerto local, arranque y parada del PostgreSQL embebido) se extraen a una biblioteca compartida.

**Informe comparativo**

- Un apartado del `README.md` que mide y compara con Blazor Server: tamaño de descarga y del paquete, tiempo hasta interactivo, comodidad del DSL frente a Razor, comportamiento de la API en escritorio y lo que no se pudo validar.

## Capabilities

### New Capabilities

- `notes-api`: API REST de notas sobre `Core`, con errores estándar, contratos que no filtran tipos de persistencia y acceso protegido en el escritorio.
- `wasm-ui`: interfaz Blazor WebAssembly escrita sin Razor que usa solo la API REST y nunca referencia `Core`, `Data` ni la base de datos.
- `wasm-desktop-host`: host de escritorio que sirve el WASM y la API en el puerto local protegido, con PostgreSQL embebido y empaquetado para Linux x64.

### Modified Capabilities

- `solution-architecture`: se añaden las reglas de dependencia de los proyectos nuevos y la prohibición de ficheros `.razor` en `Vagalume.Wasm.UI`.

## Impact

- **Repositorio**: nuevos proyectos bajo `src/` (`Vagalume.Api.Contracts`, `Vagalume.Api`, `Vagalume.Api.Client`, `Vagalume.Wasm.UI`, `Vagalume.Host.Wasm.Desktop` y la biblioteca compartida de escritorio) y bajo `tests/`. Se mueven al proyecto compartido el token, la guardia y el servicio de arranque que hoy están en `Vagalume.Host.Desktop`, con sus pruebas. Se amplía la prueba de arquitectura. Se actualizan `README.md` y `AGENTS.md`.
- **Dependencias NuGet nuevas (aprobadas por este change)**: `Microsoft.AspNetCore.Components.WebAssembly` (cliente WASM) y `Microsoft.AspNetCore.Components.WebAssembly.Server` (servir el WASM desde el host). No se añade ningún paquete de gRPC, de bibliotecas de componentes ni de pruebas de navegador.
- **Herramientas de build**: ninguna nueva respecto a `is6` (Node.js 22 o posterior para Electron.NET). No hace falta la carga de trabajo `wasm-tools` porque no se compila con AOT.
- **CI**: sin cambios; sigue sin existir (issue #3).
- **Fuera de alcance**: ver *Non-Goals* en `design.md`.
