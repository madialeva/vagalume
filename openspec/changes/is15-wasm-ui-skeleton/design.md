## Context

Ver `proposal.md` — Why para la motivación. Lo relevante para el diseño es el estado de partida, ya fusionado en `develop/v0.1.0`:

- Existen `Core` (notas, `NoteService`), `Data` (EF Core, migraciones, repositorio con un contexto por operación), `UI` (Blazor Server en una Razor Class Library), `Host.Web`, `Host.Desktop` y `Postgres.Embedded`. La arquitectura por capas y las reglas de dependencia están comprobadas por `Vagalume.Architecture.Tests`.
- `Host.Desktop` contiene el token de sesión, la guardia del puerto local (`LocalSessionGuard`), las opciones del escritorio, el servicio que arranca y para el PostgreSQL embebido y la composición con Electron.NET Core 0.6.0 (Electron 44.5.1 fijado). El informe de `is6` deja medidas de referencia en Linux: ventana dibujada a los 3,3 s en frío y 1,9 s en caliente, paquete de 133 MB comprimido y unos 524 MB desplegado.
- El informe de `is6` deja riesgos abiertos que este change hereda sin resolver: procesos huérfanos de Electron/PostgreSQL tras un cierre brusco (issue #12), Windows sin validar (issue #2) y el peso de Electron (issue #14).
- Existen paquetes `Microsoft.AspNetCore.Components.WebAssembly` y `Microsoft.AspNetCore.Components.WebAssembly.Server` 10.0.x. La compilación sin AOT no necesita ninguna carga de trabajo adicional.
- `AGENTS.md` fija: orientación a objetos, inyección manual de dependencias con una única raíz de composición, las bibliotecas no dependen de un contenedor de DI, versiones centralizadas en `Directory.Packages.props` y ningún paquete nuevo sin un change aprobado (este change aprueba los de `proposal.md` — Impact).

## Goals / Non-Goals

**Goals:**

- Averiguar con evidencia si una interfaz Blazor WebAssembly escrita sin Razor, que habla con el servidor solo por REST, funciona en el host de escritorio con Electron.NET Core.
- Comprobar que el cliente nunca ve la base de datos (ni la capa de datos) y que la lógica de servicio sigue limpia en el servidor.
- Evaluar el DSL propio: si escribir componentes sin Razor es razonablemente cómodo.
- Medir y comparar con Blazor Server: descarga y paquete, tiempo hasta interactivo, memoria y complejidad.
- Dejar un informe honesto, incluido lo que no se pudo validar.

**Non-Goals:**

- Sustituir o modificar el esqueleto de Blazor Server (`Vagalume.UI`, `Host.Web`, `Host.Desktop`): ambos conviven.
- gRPC-Web. Se elige REST para el spike; queda como alternativa con issue.
- Un host web WASM (`Vagalume.Host.Wasm.Web`) con PostgreSQL externo: el modo web con WASM se da por viable; la API es independiente del host y se podrá reutilizar.
- Un framework de componentes: enrutado de varias páginas, biblioteca de controles, sistema de estilos, formularios con validación declarativa. El DSL cubre solo lo que necesita la página de notas.
- AOT, recorte agresivo del IL, carga diferida de ensamblados y modo sin conexión (PWA).
- Autenticación y roles, despliegue en contenedores, instalador, firma y actualización automática (issues #8, #9, #13).
- Windows y macOS: el paquete y la ventana solo se verifican en Linux x64; Windows sigue en el issue #2.
- Resolver los procesos huérfanos (issue #12).
- Marcos de prueba de navegador o de componentes (bUnit, Playwright): no se añaden paquetes de pruebas.
- Un dominio de facturación.

## Decisions

### D1 — Blazor WebAssembly autónomo, sin renderizado en el servidor

`Vagalume.Wasm.UI` es un proyecto `Microsoft.NET.Sdk.BlazorWebAssembly` autónomo: un `index.html` estático y un punto de entrada que monta un componente raíz. No se usa la plantilla Blazor Web App con modo WebAssembly ni renderizado previo en el servidor, ni SignalR. El host de escritorio solo sirve ficheros estáticos y la API.

*Por qué*: es lo que modela «todo se ejecuta en el cliente y el servidor solo expone una API». El `index.html` es HTML plano, no Razor, lo cual encaja con «C# + algo de HTML y CSS». Con Blazor Web App el servidor necesitaría un componente `App` y volvería a tocar Razor.

*Alternativas descartadas*: Blazor Web App con modo automático (reintroduce Razor y dos modos de ejecución); .NET WebAssembly sin Blazor y DOM por JS interop (mucho más trabajo: enrutado, estado y renderizado propios); Avalonia/Uno (XAML, no HTML/CSS).

### D2 — Componentes como clases C# con un DSL propio

Los componentes heredan de una base propia, `MarkupComponent`, que redefine `BuildRenderTree` y delega en un único método abstracto `Render()` que devuelve un árbol de nodos inmutable (`Node`: elemento, texto, fragmento). Un conjunto de funciones estáticas (`Div`, `H1`, `Input`, `Button`…, atributos como `Class`, eventos como `OnClick`/`OnSubmit` y enlace de datos `BindValue`) construye ese árbol, y un emisor lo vuelca en el `RenderTreeBuilder` de Blazor. El estado del componente son campos normales; tras un evento Blazor vuelve a invocar `Render()`.

```csharp
protected override Node Render() =>
    Div(Class("notes"),
        H1("Notas"),
        Form(OnSubmit(CreateAsync),
            Input(BindValue(() => _newText, v => _newText = v)),
            Button("Añadir")),
        Message());
```

*Por qué*: se conserva el motor de Blazor (ciclo de vida, DI, JS interop) y se evita escribir `BuildRenderTree` a mano, que es muy verboso. Que `Render()` sea una función del estado a un árbol permite **probar la vista sin navegador**: el árbol se compara o se serializa a HTML en una prueba unitaria. Se escribe con C# normal, con refactorización y comprobación de tipos.

*Alternativas descartadas*: `BuildRenderTree` directo (verboso, propenso a errores de secuencia); emitir directamente al `RenderTreeBuilder` sin árbol intermedio (menos asignaciones, pero no se puede probar la vista sin un renderizador); una biblioteca de componentes de terceros (dependencia nueva y fuera del control del proyecto).

*Coste conocido*: el árbol intermedio asigna memoria en cada renderizado; es irrelevante para una página pequeña y se mide en el informe. Un solo componente raíz sin enrutador basta para el spike (Non-Goal).

### D3 — API REST con *minimal APIs*, contratos compartidos y errores estándar

Rutas bajo `/api/notes`: `GET` lista, `POST` crea, `PUT /{id}` modifica. Los contratos viven en `Vagalume.Api.Contracts`: `NoteDto` (identificador, texto, fecha, versión), `CreateNoteRequest`, `UpdateNoteRequest`, las constantes de ruta, la interfaz `INotesApi` y las excepciones de cliente (validación, no encontrada, conflicto). La versión de concurrencia viaja como **texto opaco**, de modo que el cliente no puede interpretarla como el `xmin` de PostgreSQL.

`Vagalume.Api` expone `MapNotesApi` como método de extensión sobre `IEndpointRouteBuilder` y convierte los errores de `Core` en respuestas estándar: 400 `ProblemDetails` (validación), 404 y 409. Las respuestas de error nunca incluyen trazas ni nombres internos.

*Por qué*: REST con `HttpClient` y `System.Text.Json` no añade paquetes y es lo más simple que cumple «el cliente nunca ve la base de datos». Que el contrato no dependa de `Core` impide por construcción que el cliente WASM arrastre `Core`, `Data` o EF Core. `Api` depende de ASP.NET Core mediante la referencia al marco, no de un contenedor de DI (cumple `AGENTS.md`).

*Alternativas descartadas*: gRPC-Web (cuatro paquetes y archivos `.proto`; Non-Goal); exponer las entidades de `Core` directamente (acopla el cliente al servidor y filtra el `xmin`).

### D4 — Cliente HTTP en un proyecto aparte

`Vagalume.Api.Client` implementa `INotesApi` con un `HttpClient` y traduce los códigos de estado a las excepciones del contrato. La interfaz permite sustituir el cliente en pruebas. El WASM crea el `HttpClient` con la dirección base de su propio origen, de modo que las peticiones son del mismo origen, sin CORS.

*Por qué*: un proyecto aparte permite probar el cliente contra un servidor real con una prueba normal de .NET, sin ejecutar WebAssembly.

### D5 — El host sirve el WASM y la API desde el mismo origen

`Vagalume.Host.Wasm.Desktop` hace referencia al proyecto WASM y sirve sus ficheros estáticos (incluida la carpeta `_framework`) con el paquete `Microsoft.AspNetCore.Components.WebAssembly.Server` y devuelve `index.html` como recurso por defecto, junto a la API de `Vagalume.Api`. Un único puerto y un único origen implican que la cookie de sesión del escritorio acompaña tanto a la descarga del WASM como a las llamadas a la API, sin CORS.

*Por qué*: es la configuración más simple y la que reutiliza la protección del puerto local de `is6` sin cambios conceptuales.

*Punto a verificar*: la descarga de los recursos de arranque de Blazor (`blazor.webassembly.js`, `dotnet.wasm`, ensamblados) debe llevar la cookie. Se espera que sí (mismo origen), pero se comprueba empíricamente en la ventana real. **Plan alternativo si no la llevara**: la guardia deja pasar solo los ficheros de `_framework`, que son públicos y no contienen datos, y sigue exigiendo la sesión a `index.html` y a la API. Se documenta como decisión en el informe si hace falta.

### D6 — Piezas comunes de escritorio en una biblioteca compartida

`SessionToken`, `LocalSessionGuard`, `DesktopOptions`, `EmbeddedDatabase` y `DesktopStartupService` pasan de `Host.Desktop` a una biblioteca `Vagalume.Desktop.Hosting`, que dependen de `Data` y `Postgres.Embedded` y de la referencia al marco de ASP.NET Core, pero no de Electron.NET. Ambos hosts de escritorio la referencian. Las pruebas existentes se actualizan con los nuevos espacios de nombres y siguen pasando.

*Por qué*: es código de seguridad; duplicarlo en dos hosts haría que un arreglo en uno no llegase al otro. Que la biblioteca no conozca Electron mantiene la regla de que solo los hosts de escritorio lo conocen.

*Alternativa descartada*: que `Host.Wasm.Desktop` referencie a `Host.Desktop` (arrastraría el empaquetado y los objetivos de MSBuild de Electron.NET de un proyecto ejecutable).

### D7 — Pruebas por capa, con PostgreSQL real y sin navegador

- `Api`: servidor real en un puerto de bucle local sobre PostgreSQL real; cubre los escenarios de `notes-api` y que las respuestas de error no filtran detalles.
- `Api.Client`: contra ese mismo servidor, por HTTP real.
- `Wasm.UI`: pruebas unitarias de las vistas como función de estado a árbol de nodos y del emisor del DSL, sin ejecutar WebAssembly.
- `Host.Wasm.Desktop`: sin Electron, con servidor real; ficheros estáticos y API con y sin sesión, persistencia, parada y rechazo de `root`.
- Arquitectura: reglas de dependencia nuevas y ausencia de `.razor`/`.cshtml`.
- Ventana y paquete: se verifican arrancando la aplicación real en un X virtual con capturas y clics, como en `is6`. Escribir texto con el teclado quedó sin poderse automatizar en `is6` (sin gestor de ventanas); si no se encuentra un método, queda como limitación del informe.

### D8 — Qué se mide y con qué se compara

Se registran, en Linux x64: tamaño de la carpeta `_framework` publicada (sin comprimir y con Brotli), nivel de ensamblados descargados, tiempo hasta que la lista de notas aparece en la ventana en frío y en caliente, memoria residente del conjunto de procesos de la aplicación en reposo y tamaño del paquete empaquetado. Todo frente a las cifras de Blazor Server de `is6`.

## Risks / Trade-offs

- **Tamaño y arranque del WASM**: el runtime de .NET en el navegador pesa varios MB y arranca más lento que una página renderizada por el servidor. → Se mide; no se optimiza en este change.
- **Dos runtimes de .NET en escritorio** (el host y el del renderizador) y por tanto más memoria. → Se mide la memoria frente a Blazor Server.
- **Ergonomía del DSL**: puede resultar peor que Razor para marcados grandes. → Se evalúa honestamente con la página de notas; es parte del resultado.
- **La cookie de sesión y la descarga de arranque**: ver D5, con plan alternativo.
- **CSRF sobre la API del escritorio**: la API usa una cookie. → Mitigado por `SameSite=Strict`, la comprobación del nombre de `Host` y por exigir contenido JSON en las escrituras.
- **Electron.NET Core 0.x, Electron antiguo por defecto y procesos huérfanos**: heredados de `is6` (issues #12 y #14). → Mismas mitigaciones y mismo registro en el informe.
- **Prueba de la interfaz limitada**: sin navegador automatizado, la interacción real solo se verifica a mano y por la prueba de vistas. → Se documenta.
- **Prototipo desechable**: el código no se escribe con calidad de producción, pero respeta `AGENTS.md`.

## Open Questions

Ninguna que cambie las specs o las tareas.
