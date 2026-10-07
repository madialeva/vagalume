## Purpose

Definir las capas de la solución y las reglas de dependencia entre proyectos, para que la interfaz y la lógica puedan reutilizarse en el host web y en el host de escritorio sin reescribirse.

## ADDED Requirements

### Requirement: La solución se organiza en capas con dependencias en un solo sentido

La solución SHALL contener los proyectos `Core` (dominio y casos de uso), `Data` (persistencia), `UI` (componentes y páginas Blazor en una Razor Class Library), `Host.Web` y `Host.Desktop`. `Core` SHALL NOT depender de ningún otro proyecto de la solución ni de EF Core, Blazor o Electron.NET. `Data` y `UI` SHALL depender únicamente de `Core` entre los proyectos de la solución. `UI` SHALL NOT depender de `Data`, de ningún host ni de Electron.NET.

#### Scenario: Las referencias respetan las reglas

- **WHEN** se examinan las referencias entre proyectos y los paquetes de cada proyecto
- **THEN** ninguna referencia SHALL violar las reglas de dependencia de las capas

#### Scenario: Una referencia prohibida se detecta

- **WHEN** un proyecto añade una referencia que viola las reglas, por ejemplo `Core` hacia `Data`
- **THEN** la prueba de arquitectura SHALL fallar nombrando la referencia infractora

### Requirement: Los hosts son la única raíz de composición

Cada host SHALL construir las dependencias de la aplicación en un único punto de arranque. Los proyectos `Core`, `Data` y `UI` SHALL NOT depender de un contenedor de inyección de dependencias ni resolver servicios por sí mismos. Solo `Host.Desktop` SHALL conocer Electron.NET y el PostgreSQL embebido.

#### Scenario: Los dos hosts comparten la interfaz

- **WHEN** se arranca `Host.Web` o `Host.Desktop`
- **THEN** ambos SHALL servir la misma interfaz definida en `UI`, sin duplicar componentes ni páginas

### Requirement: La interfaz no usa una API REST propia

Los componentes de `UI` SHALL acceder a la lógica mediante los servicios de `Core` inyectados en el servidor. La solución SHALL NOT exponer una API REST entre la interfaz y la lógica.

#### Scenario: La interfaz usa solo el canal interactivo del servidor

- **WHEN** un usuario crea y lista notas desde la interfaz
- **THEN** la operación SHALL completarse por el canal interactivo de Blazor Server sin que la aplicación defina ningún punto de entrada REST de negocio
