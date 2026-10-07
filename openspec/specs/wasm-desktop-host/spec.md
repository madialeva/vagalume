# wasm-desktop-host Specification

## Purpose
Describir el host de escritorio de la variante WebAssembly: sirve el WASM y la API REST en el puerto local protegido, con su PostgreSQL embebido, dentro de una ventana de Electron.NET Core.

## Requirements

### Requirement: El host sirve el WASM y la API en el puerto local

El host de escritorio WASM SHALL arrancar el PostgreSQL embebido en el perfil del usuario, aplicar las migraciones, y servir en un puerto local de `127.0.0.1` tanto los ficheros estáticos de la interfaz WebAssembly como la API REST, y SHALL mostrarlos en una ventana de Electron.NET Core. El usuario SHALL poder usar la aplicación sin configurar ni instalar nada más.

#### Scenario: Primer arranque

- **WHEN** el usuario abre la aplicación y no existe su directorio de datos
- **THEN** SHALL crearse el clúster, aplicarse las migraciones y mostrarse una ventana cuya interfaz WebAssembly carga y muestra la lista de notas obtenida de la API

#### Scenario: Crear una nota desde la ventana

- **WHEN** el usuario crea una nota en la ventana
- **THEN** la nota SHALL persistir y SHALL seguir apareciendo tras cerrar y volver a abrir la aplicación

### Requirement: El puerto local queda protegido para el WASM y para la API

El host SHALL generar un token aleatorio en cada arranque, SHALL rechazar toda petición sin sesión válida (ficheros estáticos y API), y la ventana SHALL obtener acceso sin intervención del usuario de modo que el WASM pueda llamar a la API desde el navegador embebido. El token SHALL NOT escribirse en ficheros ni en los registros.

#### Scenario: Petición sin sesión

- **WHEN** un cliente local pide al puerto de la aplicación cualquier fichero o llamada de la API sin sesión válida
- **THEN** el host SHALL responder 403 sin contenido

#### Scenario: El WASM llama a la API con la sesión de la ventana

- **WHEN** la interfaz WebAssembly arranca dentro de la ventana
- **THEN** sus llamadas a la API SHALL aceptarse sin presentar el token en cada petición

### Requirement: Cerrar la aplicación detiene la base de datos

Al cerrar la ventana principal, el host SHALL detener el servidor web y el PostgreSQL embebido de forma limpia y SHALL NOT dejar procesos huérfanos tras un cierre normal.

#### Scenario: Cierre normal

- **WHEN** el usuario cierra la ventana principal
- **THEN** los procesos del host, de Electron y del servidor PostgreSQL SHALL terminar

### Requirement: Paquete ejecutable para Linux x64

El proyecto SHALL producir un paquete ejecutable de la aplicación de escritorio WASM para Linux x64 que incluya únicamente los binarios de PostgreSQL de esa plataforma y funcione sin red, sin .NET y sin Node.js instalados.

#### Scenario: Ejecución del paquete

- **WHEN** se extrae el paquete y se ejecuta en un equipo sin red y con un `PATH` sin `dotnet` ni `node`
- **THEN** la aplicación SHALL arrancar, crear su clúster y mostrar la interfaz WebAssembly operativa

### Requirement: La ejecución como root se rechaza con un error claro

En Linux, si el proceso se ejecuta como `root`, el host SHALL fallar antes de arrancar el servidor de base de datos con un error que indique que no puede ejecutarse como `root`.

#### Scenario: Ejecución como root

- **WHEN** el usuario `root` abre la aplicación en Linux
- **THEN** SHALL informar de que no puede ejecutarse como `root` y SHALL NOT arrancar el servidor de base de datos
