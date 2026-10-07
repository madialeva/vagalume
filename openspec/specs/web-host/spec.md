# web-host Specification

## Purpose
Describir el host web: el mismo ASP.NET Core que sirve la interfaz compartida, desplegable en un servidor y usado desde un navegador, con un PostgreSQL externo.

## Requirements

### Requirement: El host web sirve la interfaz con un PostgreSQL externo

El host web SHALL obtener la cadena de conexión de la configuración de la aplicación (incluidas las variables de entorno), aplicar las migraciones al arrancar y servir la interfaz de notas a los navegadores. El host web SHALL NOT arrancar ni empaquetar ningún PostgreSQL embebido.

#### Scenario: Arranque con una base de datos disponible

- **WHEN** el host web arranca con una cadena de conexión válida hacia un PostgreSQL en marcha
- **THEN** SHALL servir la página de notas y las notas creadas desde el navegador SHALL persistir en esa base de datos

#### Scenario: Falta la cadena de conexión

- **WHEN** el host web arranca sin cadena de conexión configurada
- **THEN** SHALL terminar con un error que indique qué clave de configuración falta y SHALL NOT servir ninguna página

#### Scenario: La base de datos no está disponible

- **WHEN** el host web arranca con una cadena de conexión que no puede conectarse
- **THEN** SHALL terminar con un error que indique que no se pudo conectar a la base de datos

### Requirement: Varios navegadores usan la misma instalación

El host web SHALL atender a varios navegadores simultáneos sobre la misma base de datos, y los datos creados desde uno SHALL ser visibles desde los demás.

#### Scenario: Dos sesiones simultáneas

- **WHEN** dos navegadores abren la aplicación y uno crea una nota
- **THEN** el otro SHALL verla al recargar la lista

### Requirement: El host web no incluye autenticación en este esqueleto

Este esqueleto SHALL documentar que el host web no autentica a los usuarios y que, por tanto, no debe exponerse a una red no confiable.

#### Scenario: Advertencia documentada

- **WHEN** se lee la documentación del host web
- **THEN** SHALL indicar que no hay autenticación y que no debe publicarse en internet tal como está
