## ADDED Requirements

### Requirement: Los proyectos de la variante WebAssembly respetan reglas de dependencia propias

`Api.Contracts` SHALL NOT depender de ningún otro proyecto de la solución ni de EF Core, Npgsql o Electron.NET. `Api` SHALL depender únicamente de `Core` y `Api.Contracts`. `Api.Client` SHALL depender únicamente de `Api.Contracts`. `Wasm.UI` SHALL depender únicamente de `Api.Contracts` y `Api.Client` y SHALL NOT depender de `Core`, `Data`, EF Core, Npgsql ni Electron.NET. Solo `Host.Wasm.Desktop` SHALL conocer Electron.NET y el PostgreSQL embebido de entre los proyectos de esta variante.

#### Scenario: Las referencias respetan las reglas

- **WHEN** se examinan las referencias entre proyectos y los paquetes de cada proyecto de la variante WebAssembly
- **THEN** ninguna referencia SHALL violar estas reglas y la prueba de arquitectura SHALL fallar nombrando cualquier referencia infractora

### Requirement: La interfaz WebAssembly no contiene ficheros Razor

La prueba de arquitectura SHALL fallar si `Wasm.UI` contiene algún fichero `.razor` o `.cshtml`.

#### Scenario: Un fichero Razor se detecta

- **WHEN** se añade un fichero `.razor` al proyecto `Wasm.UI`
- **THEN** la prueba de arquitectura SHALL fallar nombrando el fichero
