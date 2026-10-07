## Purpose

Definir la API REST de notas que consume la interfaz WebAssembly: contratos estables, errores estándar y ninguna filtración de tipos de persistencia al cliente.

## ADDED Requirements

### Requirement: La API permite listar, crear y modificar notas

La API SHALL permitir listar las notas en orden de creación, crear una nota con un texto y modificar el texto de una nota existente indicando la versión que el cliente leyó. Cada nota devuelta SHALL incluir su identificador, su texto, su fecha de creación y su versión como valor opaco.

#### Scenario: Crear y listar una nota

- **WHEN** un cliente crea una nota con texto «Hola» y después lista las notas
- **THEN** la creación SHALL responder con la nota creada y la lista SHALL incluir una nota con ese texto

#### Scenario: Modificar con la versión leída

- **WHEN** un cliente modifica el texto de una nota enviando la versión que leyó
- **THEN** la API SHALL responder con la nota actualizada y una versión distinta de la anterior

### Requirement: Los errores se devuelven como respuestas HTTP estándar

La API SHALL responder 400 con `ProblemDetails` cuando el texto de una nota no es válido, 404 cuando la nota no existe y 409 cuando la versión enviada ya no es la actual. Una respuesta de error SHALL NOT incluir trazas de pila, nombres de tipos internos ni detalles de la base de datos.

#### Scenario: Texto vacío

- **WHEN** un cliente crea una nota con texto vacío o solo espacios
- **THEN** la API SHALL responder 400 con un mensaje de validación y SHALL NOT guardar la nota

#### Scenario: Nota inexistente

- **WHEN** un cliente modifica una nota que no existe
- **THEN** la API SHALL responder 404

#### Scenario: Conflicto de concurrencia

- **WHEN** dos clientes leen la misma nota, el primero la modifica y el segundo intenta modificarla con la versión que leyó
- **THEN** la API SHALL responder 409 al segundo y la nota SHALL conservar el texto del primero

### Requirement: Los contratos no filtran tipos de persistencia

Los contratos de la API SHALL estar definidos en un proyecto que no depende de `Core`, de EF Core ni de Npgsql, y SHALL representar la versión de concurrencia como un valor opaco que el cliente no interpreta.

#### Scenario: El contrato se compila sin dependencias del servidor

- **WHEN** se examinan las referencias del proyecto de contratos
- **THEN** SHALL NOT incluir ninguna referencia a `Core`, `Data`, EF Core ni Npgsql

### Requirement: La API del escritorio solo atiende a la ventana de la aplicación

En el host de escritorio, la API SHALL estar protegida por el mismo token de sesión que el resto del puerto local: una petición a la API sin una sesión válida SHALL ser rechazada con 403 y sin cuerpo.

#### Scenario: Llamada a la API sin sesión

- **WHEN** un proceso local llama a la API del host de escritorio sin presentar una sesión válida
- **THEN** la API SHALL responder 403 sin ningún dato de notas
