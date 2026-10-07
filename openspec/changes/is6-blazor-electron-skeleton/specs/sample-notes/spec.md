## Purpose

Demostrar el recorrido completo interfaz, casos de uso, persistencia y PostgreSQL con un modelo mínimo de notas, incluida la concurrencia optimista, sin construir ningún dominio de facturación.

## ADDED Requirements

### Requirement: Las notas se crean y se listan de forma persistente

El sistema SHALL permitir crear una nota con un texto no vacío y listar las notas existentes en orden de creación. Las notas SHALL persistir en PostgreSQL y SHALL seguir disponibles después de reiniciar la aplicación.

#### Scenario: Crear y listar una nota

- **WHEN** un usuario crea una nota con texto «Hola»
- **THEN** la lista de notas SHALL incluir una nota con ese texto

#### Scenario: Persistencia tras reiniciar

- **WHEN** la aplicación se detiene y se arranca de nuevo sobre la misma base de datos
- **THEN** las notas creadas antes SHALL seguir en la lista

#### Scenario: Una nota vacía se rechaza

- **WHEN** un usuario intenta crear una nota con texto vacío o solo espacios
- **THEN** el sistema SHALL rechazar la operación con un mensaje de validación y SHALL NOT guardar la nota

### Requirement: La edición concurrente se detecta

El sistema SHALL detectar que una nota ha sido modificada por otra operación desde que se leyó, usando el token de concurrencia optimista de PostgreSQL, y SHALL rechazar la modificación obsoleta en lugar de sobrescribir silenciosamente.

#### Scenario: Modificación obsoleta

- **WHEN** dos usuarios leen la misma nota, el primero la modifica y el segundo intenta modificarla después con la versión que leyó
- **THEN** el sistema SHALL rechazar la segunda modificación con un error de conflicto y la nota SHALL conservar el texto del primero

### Requirement: El esquema se crea con una única cadena de migraciones

El esquema de la base de datos SHALL crearse y actualizarse con una única cadena de migraciones, la misma para el host web y el de escritorio, aplicada al arrancar.

#### Scenario: Base de datos nueva

- **WHEN** un host arranca contra una base de datos vacía
- **THEN** el esquema SHALL crearse con las migraciones y la aplicación SHALL poder guardar notas

#### Scenario: Arranque repetido

- **WHEN** un host arranca contra una base de datos que ya tiene todas las migraciones aplicadas
- **THEN** SHALL NOT modificar el esquema ni los datos existentes
