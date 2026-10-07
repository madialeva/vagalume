## Purpose

Definir la interfaz Blazor WebAssembly alternativa: escrita en C# sin Razor, que habla con el servidor solo por la API REST y nunca ve la base de datos.

## ADDED Requirements

### Requirement: La interfaz ofrece las mismas operaciones que la versión de Blazor Server

La interfaz WebAssembly SHALL permitir crear notas con validación, listarlas y editarlas, y SHALL mostrar un mensaje comprensible cuando el servidor rechaza un texto no válido, cuando la nota ya no existe y cuando otra persona la ha modificado.

#### Scenario: Crear y ver una nota

- **WHEN** un usuario escribe un texto y pulsa añadir
- **THEN** la nota SHALL aparecer en la lista tras la respuesta del servidor

#### Scenario: Texto vacío

- **WHEN** un usuario pulsa añadir con el texto vacío
- **THEN** la interfaz SHALL mostrar el mensaje de validación y SHALL NOT añadir ninguna nota

#### Scenario: Conflicto al editar

- **WHEN** el servidor responde 409 al guardar una edición
- **THEN** la interfaz SHALL informar del conflicto, recargar la lista y SHALL NOT sobrescribir el cambio de la otra persona

### Requirement: La interfaz no usa sintaxis Razor

El proyecto de la interfaz WebAssembly SHALL declarar sus componentes como clases C# y SHALL NOT contener ficheros `.razor` ni `.cshtml`. El marcado HTML SHALL expresarse mediante el DSL propio del proyecto.

#### Scenario: Sin ficheros Razor

- **WHEN** se examina el contenido del proyecto de la interfaz
- **THEN** no SHALL existir ningún fichero con extensión `.razor` ni `.cshtml`

### Requirement: El cliente nunca ve la base de datos

El proyecto de la interfaz WebAssembly SHALL depender únicamente de los contratos de la API y del cliente HTTP. SHALL NOT depender de `Core`, `Data`, EF Core, Npgsql ni del PostgreSQL embebido, y su ensamblado SHALL NOT incluir esos componentes.

#### Scenario: El paquete descargable no contiene la capa de datos

- **WHEN** se examinan los ensamblados que el navegador descarga para arrancar la interfaz
- **THEN** SHALL NOT incluir `Vagalume.Core`, `Vagalume.Data`, EF Core ni Npgsql

### Requirement: Toda comunicación con el servidor pasa por la API REST

La interfaz SHALL comunicarse con el servidor únicamente mediante peticiones HTTP a la API REST de notas del mismo origen, sin canales persistentes.

#### Scenario: Sin conexión persistente

- **WHEN** la interfaz está abierta y sin actividad
- **THEN** SHALL NOT mantener ninguna conexión interactiva abierta con el servidor
