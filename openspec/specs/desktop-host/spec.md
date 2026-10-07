# desktop-host Specification

## Purpose
Describir el host de escritorio: la misma aplicación dentro de una ventana de Electron.NET Core, con su propio PostgreSQL embebido, un puerto local protegido y paquetes ejecutables para Linux x64 y Windows x64.

## Requirements

### Requirement: El host de escritorio arranca la base de datos y la ventana

El host de escritorio SHALL arrancar el PostgreSQL embebido con el directorio de datos en el perfil del usuario, aplicar las migraciones, servir la interfaz en un puerto local de `127.0.0.1` y mostrarla en una ventana de Electron.NET Core. El usuario SHALL poder usar la aplicación sin configurar ni instalar nada más.

#### Scenario: Primer arranque

- **WHEN** el usuario abre la aplicación de escritorio y no existe su directorio de datos
- **THEN** SHALL crearse el clúster, aplicarse las migraciones y mostrarse una ventana con la página de notas operativa

#### Scenario: Arranque posterior

- **WHEN** el usuario abre la aplicación otra vez
- **THEN** SHALL reutilizarse el clúster existente y las notas anteriores SHALL aparecer

### Requirement: Cerrar la aplicación detiene la base de datos

Al cerrar la ventana principal, el host de escritorio SHALL detener el servidor y el PostgreSQL embebido de forma limpia, y SHALL NOT dejar procesos huérfanos.

#### Scenario: Cierre normal

- **WHEN** el usuario cierra la ventana principal
- **THEN** los procesos del host, de Electron y del servidor PostgreSQL SHALL terminar y el directorio de datos SHALL quedar sin servidor en marcha

### Requirement: El puerto local queda protegido por un token de sesión

El host de escritorio SHALL generar un token aleatorio en cada arranque y SHALL rechazar toda petición al puerto local que no lo presente, de modo que otro proceso de la máquina no pueda usar la aplicación. La ventana de la aplicación SHALL obtener acceso con ese token sin intervención del usuario. El token SHALL NOT escribirse en ficheros ni en los registros.

#### Scenario: Petición sin token

- **WHEN** un cliente local pide cualquier página al puerto de la aplicación sin presentar el token
- **THEN** el host SHALL rechazar la petición y SHALL NOT revelar ningún contenido de la aplicación

#### Scenario: Petición con un token incorrecto

- **WHEN** un cliente presenta un token distinto del generado en este arranque
- **THEN** el host SHALL rechazar la petición

#### Scenario: La ventana accede con el token

- **WHEN** la ventana de la aplicación se abre
- **THEN** SHALL poder usar la interfaz, incluido el canal interactivo, y las peticiones posteriores SHALL aceptarse sin volver a presentar el token en la dirección

#### Scenario: El token cambia en cada arranque

- **WHEN** la aplicación se cierra y se abre de nuevo
- **THEN** el token SHALL ser distinto y el anterior SHALL ser rechazado

### Requirement: Paquetes ejecutables por plataforma

El proyecto SHALL producir un paquete ejecutable de la aplicación de escritorio para Linux x64 y otro para Windows x64. Cada paquete SHALL incluir únicamente los binarios de PostgreSQL de su plataforma y SHALL funcionar sin descargar nada en ejecución.

#### Scenario: Paquete de Linux

- **WHEN** se genera el paquete para Linux x64 y se ejecuta en un equipo sin red y sin .NET ni Node.js instalados
- **THEN** la aplicación SHALL arrancar, crear su clúster y mostrar la página de notas operativa

#### Scenario: Cada paquete lleva una sola plataforma

- **WHEN** se inspecciona el contenido de un paquete
- **THEN** SHALL contener los binarios de PostgreSQL de su plataforma y SHALL NOT contener los de la otra

### Requirement: La ejecución como root se rechaza con un error claro

En Linux, si el proceso se ejecuta como `root`, el host de escritorio SHALL fallar antes de arrancar el servidor de base de datos con un error que indique que no puede ejecutarse como `root`.

#### Scenario: Ejecución como root

- **WHEN** el usuario `root` abre la aplicación de escritorio en Linux
- **THEN** SHALL informar de que no puede ejecutarse como `root` y SHALL NOT arrancar el servidor de base de datos
