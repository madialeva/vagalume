## 1. Solución, contratos y reglas de arquitectura

- [x] 1.1 Crear los proyectos `Vagalume.Api.Contracts`, `Vagalume.Api`, `Vagalume.Api.Client` bajo `src/` con las referencias de D3 y D4, añadir las versiones de los paquetes nuevos a `Directory.Packages.props` y añadirlos a `Vagalume.slnx`. Verificación: `dotnet build` pasa sin advertencias.
- [x] 1.2 Ampliar la prueba de arquitectura con las reglas de `solution-architecture` para los proyectos nuevos, incluida la comprobación de que `Wasm.UI` no contiene ficheros `.razor` ni `.cshtml`. Verificación: la prueba pasa con la solución real y falla, nombrando la infracción, con un ejemplo que viole una regla de referencias y con otro que contenga un `.razor`.

## 2. Contratos y API REST

- [x] 2.1 Implementar en `Api.Contracts` los DTOs, las constantes de ruta, `INotesApi` y las excepciones de cliente, con la versión como texto opaco. Verificación: `dotnet build` y la prueba de arquitectura confirman que el proyecto no depende de nada.
- [x] 2.2 Implementar en `Api` `MapNotesApi` con listar, crear y modificar sobre `NoteService`, y la traducción de errores a 400 `ProblemDetails`, 404 y 409 sin detalles internos. Verificación: `dotnet build` pasa.
- [x] 2.3 Pruebas de la API con servidor real y PostgreSQL real: crear y listar, modificar con la versión leída, texto vacío (400), nota inexistente (404), conflicto con dos lecturas (409) y que ninguna respuesta de error contiene trazas, nombres de tipos internos ni texto de la base de datos. Verificación: las pruebas pasan y cubren los escenarios de `notes-api`.

## 3. Cliente HTTP

- [x] 3.1 Implementar en `Api.Client` `NotesApiClient` sobre `HttpClient` con la traducción de códigos de estado a las excepciones del contrato. Verificación: `dotnet build` pasa y la prueba de arquitectura confirma que solo depende de `Api.Contracts`.
- [x] 3.2 Pruebas del cliente contra el servidor real de 2.3: crear, listar, modificar, validación, no encontrada y conflicto traducidos a la excepción correcta. Verificación: las pruebas pasan.

## 4. Interfaz WebAssembly sin Razor

- [x] 4.1 Antes de construir el DSL, verificar que un proyecto Blazor WebAssembly autónomo compila en este entorno y que sus ficheros de arranque se publican y se sirven desde un host ASP.NET Core con `Microsoft.AspNetCore.Components.WebAssembly.Server` (mínimo desechable o el propio `Wasm.UI` vacío). Verificación: se obtiene `index.html` y `_framework/blazor.webassembly.js` por HTTP, y las notas del resultado quedan para el informe; si no es viable, se detiene el trabajo y se piden indicaciones.
- [x] 4.2 Implementar el DSL: `Node`, funciones de elementos y atributos, eventos, enlace de datos, `MarkupComponent` y el emisor sobre `RenderTreeBuilder`. Verificación: pruebas unitarias del árbol y del emisor (elementos anidados, atributos, texto escapado, evento y enlace producen la secuencia esperada).
- [x] 4.3 Implementar `Wasm.UI`: `index.html`, estilos, punto de entrada con el `HttpClient` del mismo origen, la página de notas con el DSL (alta con validación, lista, edición con mensaje de conflicto) y el uso de `INotesApi`. Verificación: `dotnet build` pasa y pruebas unitarias de la vista de la página con un `INotesApi` falso cubren los escenarios de `wasm-ui` (texto vacío, creación, conflicto).
- [x] 4.4 Comprobar que el cliente nunca ve la base de datos: la salida publicada de `Wasm.UI` no contiene `Vagalume.Core`, `Vagalume.Data`, EF Core ni Npgsql, y la prueba de arquitectura sigue en verde. Verificación: listado de los ensamblados publicados y la prueba de arquitectura.

## 5. Piezas comunes de escritorio

- [ ] 5.1 Crear `Vagalume.Desktop.Hosting` y mover a él `SessionToken`, `LocalSessionGuard`, `DesktopOptions`, `EmbeddedDatabase` y `DesktopStartupService`, haciendo que `Host.Desktop` y sus pruebas lo usen y añadiendo la regla de arquitectura correspondiente. Verificación: `dotnet build` y las 10 pruebas de `Host.Desktop.Tests` y las de arquitectura pasan sin cambios de comportamiento.

## 6. Host de escritorio WASM

- [ ] 6.1 Implementar la clase del servidor web de `Host.Wasm.Desktop` sin Electron: guardia de sesión, ficheros estáticos del WASM con `index.html` por defecto, `MapNotesApi`, PostgreSQL embebido, migraciones, rechazo de `root` y parada ordenada. Verificación: pruebas con servidor real: sin sesión todo da 403 sin contenido, con sesión se sirven `index.html` y los ficheros de arranque, la API responde con la cookie de sesión, las notas persisten entre arranques, se rechaza `root` y la parada deja sin servidor PostgreSQL.
- [ ] 6.2 Integrar Electron.NET Core en el `Program` del host WASM (ventana con la dirección que lleva el token, parada de servidor y base de datos al cerrar) reutilizando el patrón de `is6`. Verificación: arrancar con `dotnet run` abre la ventana.
- [ ] 6.3 Verificar en la ventana real (X virtual): el WASM carga con la cookie de sesión (comprobando la incógnita de D5), la lista aparece obtenida de la API y un clic en añadir con el texto vacío muestra el mensaje de validación del servidor; al cerrar no queda ningún proceso. Verificación: capturas y resultado registrados para el informe, con el plan alternativo de D5 aplicado solo si hizo falta.
- [ ] 6.4 Documentar en `README.md` cómo ejecutar el host de escritorio WASM. Verificación: seguir las instrucciones en limpio abre la ventana.

## 7. Empaquetado y medidas

- [ ] 7.1 Configurar el empaquetado para `linux-x64` del host WASM con solo los binarios de PostgreSQL de Linux y los ficheros del WASM publicados. Verificación: el comando documentado genera el paquete y su contenido incluye los binarios de Linux, los ficheros de `_framework` y ningún ensamblado de `Core`/`Data` en el cliente.
- [ ] 7.2 Verificar el paquete en modo estricto (sin red, sin root, `PATH` sin `dotnet` ni `node`): primer arranque, interfaz WASM operativa y cierre limpio. Verificación: resultado registrado.
- [ ] 7.3 Medir lo definido en D8 y compararlo con las cifras de Blazor Server de `is6`. Verificación: cifras registradas en el informe.

## 8. Informe comparativo y documentación

- [ ] 8.1 Añadir al `README.md` el informe de este change: resultado de cada prueba, evaluación del DSL con ejemplos frente a Razor, comparación medida con Blazor Server, lo que no se pudo validar y una recomendación fundada sobre si WASM es viable en escritorio y si merece la pena frente a Blazor Server. Verificación: cada afirmación corresponde a una prueba o a una medición realizada.
- [ ] 8.2 Actualizar `AGENTS.md` (proyectos nuevos, comandos, reglas de dependencia y estado). Verificación: `dotnet format --verify-no-changes && dotnet build && dotnet test` en verde y los documentos coinciden con el código.
