## 1. Solución, proyectos de apoyo y prueba de arquitectura

- [x] 1.1 Crear los proyectos `Vagalume.Core`, `Vagalume.Data`, `Vagalume.UI` (Razor Class Library), `Vagalume.Host.Web` y `Vagalume.Host.Desktop` bajo `src/`, añadirlos a `Vagalume.slnx` con solo las referencias de proyecto permitidas por D2. Verificación: `dotnet build` pasa sin advertencias.
- [x] 1.2 Extraer `TestEnvironment` y `TestCluster` de `Vagalume.Postgres.Embedded.Tests` a un proyecto de apoyo `tests/Vagalume.Testing` y hacer que las pruebas existentes lo usen. Verificación: `dotnet test` mantiene las 16 pruebas del change anterior en verde.
- [x] 1.3 Escribir la prueba de arquitectura que lee los `.csproj` y comprueba las reglas de dependencia de las capas (referencias de proyecto y de paquete), con un caso que demuestre que una referencia prohibida hace fallar la prueba y nombra la referencia. Verificación: la prueba pasa con la solución real y falla con un `.csproj` de ejemplo que viole una regla.

## 2. Core y Data

- [x] 2.1 Implementar en `Core` la entidad `Note`, la interfaz de repositorio y el servicio de casos de uso (crear con validación de texto no vacío, listar, modificar con comprobación de versión), con el error de dominio de conflicto de concurrencia. Verificación: pruebas unitarias de validación, orden de listado y conflicto, con un repositorio en memoria de prueba.
- [x] 2.2 Añadir a `Data` el `DbContext` con el modelo de `Note`, el `xmin` como token de concurrencia y la implementación del repositorio con EF Core y Npgsql; fijar `dotnet-ef` en un manifiesto de herramientas local y crear la migración inicial. Verificación: `dotnet ef migrations list` muestra la migración y `dotnet build` pasa.
- [x] 2.3 Pruebas de integración de `Data` contra un PostgreSQL real (`Vagalume.Testing`): migrar una base vacía, crear y listar notas, persistencia al reiniciar el servidor, arranque repetido sin cambios y conflicto de `xmin` con dos contextos. Verificación: las pruebas pasan y cubren los escenarios de `sample-notes`.

## 3. Interfaz compartida

- [x] 3.1 Implementar en `UI` los componentes de la página de notas (formulario de alta con validación, lista, edición con mensaje de conflicto) que inyectan los servicios de `Core`, y el componente de enrutado de la aplicación con modo de renderizado interactivo de servidor. Verificación: `dotnet build` pasa y la prueba de arquitectura confirma que `UI` solo depende de `Core`.

## 4. Host web

- [x] 4.1 Implementar `Host.Web`: raíz de composición, lectura de la cadena de conexión de la configuración con error claro si falta o no conecta, aplicación de migraciones al arrancar y servicio de la interfaz de `UI`. Verificación: arrancar contra un PostgreSQL de prueba y comprobar que la página de notas responde; sin cadena de conexión el proceso termina con el mensaje esperado.
- [x] 4.2 Pruebas del host web con `WebApplicationFactory` contra un PostgreSQL real: arranque válido, falta de cadena de conexión, base de datos inalcanzable y dos sesiones simultáneas sobre los mismos datos. Verificación: las pruebas pasan y cubren los escenarios de `web-host`.
- [x] 4.3 Documentar en `README.md` cómo ejecutar el host web contra un PostgreSQL externo (incluida la clave de configuración) y la advertencia de que no autentica y no debe exponerse a internet. Verificación: seguir las instrucciones del README en limpio arranca el host web.

## 5. Verificación de Electron.NET Core

- [ ] 5.1 Antes de construir sobre él, verificar en un proyecto desechable (fuera de la solución o descartado después) cómo se comporta `ElectronNET.Core` 0.6.0 con .NET 10: cómo se lanzan .NET y Electron, cómo se abre una ventana con una dirección propia, qué ocurre al cerrar la ventana y al matar cada proceso, cómo se pasa la dirección a la ventana (¿línea de órdenes?) y qué necesita en Linux (*sandbox*, pantalla). Verificación: notas de hallazgos en el informe del README y decisión explícita de continuar o detenerse y pedir indicaciones si no es viable.

## 6. Host de escritorio

- [ ] 6.1 Implementar la clase del servidor web de escritorio (sin Electron): rechazo de `root` por la estrategia de plataforma, arranque del PostgreSQL embebido en el directorio de datos del perfil del usuario, migraciones, Kestrel en `127.0.0.1` con puerto libre y parada ordenada de servidor y base de datos. Verificación: prueba con `WebApplicationFactory` o de integración que arranca, sirve la página de notas, para y comprueba que no queda servidor PostgreSQL en marcha.
- [ ] 6.2 Implementar el middleware del token de sesión de D6 (comprobación de `Host`, cookie con comparación en tiempo constante, intercambio del parámetro por la cookie con redirección, 403 sin cuerpo) y el generador del token por arranque, sin escribirlo en ficheros ni registros. Verificación: pruebas de los cuatro escenarios de `desktop-host` (sin token, token incorrecto, acceso con token incluido el canal interactivo, token distinto en cada arranque) y de `Host` ajeno.
- [ ] 6.3 Integrar Electron.NET Core en `Program`: abrir la ventana con la dirección que lleva el token, y detener servidor y base de datos al cerrar la ventana principal. Verificación: arrancar con `dotnet run`/herramienta de Electron.NET y comprobar con una ventana real el primer arranque, el arranque posterior con las notas anteriores y que al cerrar no queda ningún proceso de host, Electron ni PostgreSQL; si no hay pantalla, queda como comprobación manual del usuario con el método anotado.
- [ ] 6.4 Probar el cierre brusco con la aplicación completa: matar el host y Electron con una señal forzada, abrir de nuevo y comprobar que se reutiliza o recupera el servidor y que las notas confirmadas siguen ahí. Verificación: prueba automatizada del servidor sin Electron y comprobación con ventana real registrada en el informe.
- [ ] 6.5 Documentar en `README.md` cómo ejecutar el host de escritorio desde las fuentes, los requisitos (Node.js 22 o posterior) y la advertencia de no ejecutarlo como `root`. Verificación: seguir las instrucciones del README en limpio abre la ventana.

## 7. Empaquetado

- [ ] 7.1 Configurar el empaquetado de Electron.NET Core para `linux-x64`, que copie dentro del paquete solo `artifacts/postgres/linux-amd64/` y haga que la aplicación localice los binarios relativos a su propio directorio. Verificación: el paquete se genera con un comando documentado y su contenido incluye los binarios de Linux y ninguno de Windows.
- [ ] 7.2 Verificar el paquete de Linux: ejecutarlo sin .NET ni Node.js en el `PATH` y sin red (por ejemplo con un espacio de red sin interfaces externas, como en el change anterior), comprobando primer arranque, página de notas y cierre limpio. Verificación: resultado registrado; si no hay pantalla, comprobación manual del usuario con el método anotado.
- [ ] 7.3 Configurar el empaquetado de `win-x64` con solo `artifacts/postgres/windows-amd64/`, y generarlo si la herramienta permite compilarlo desde Linux. Verificación: el paquete se genera y su contenido solo lleva los binarios de Windows, o queda documentado por qué no se pudo generar; en ambos casos se marca como **sin validar** (issue #2).
- [ ] 7.4 Medir tamaño de cada paquete y tiempos de arranque en frío y en caliente de la aplicación empaquetada para el informe. Verificación: cifras registradas en el informe.

## 8. Informe de hallazgos y documentación

- [ ] 8.1 Añadir al `README.md` un informe de hallazgos de este change: estabilidad y limitaciones de Electron.NET Core 0.x, ciclo de vida y cierre brusco, alcance real de la protección del token, tamaños y tiempos, resultado de cada prueba por plataforma, lo que no se pudo validar (Windows, ventana si no hubo pantalla) y una recomendación fundada sobre si Blazor Server con Electron.NET es viable como base. Verificación: cada afirmación del informe corresponde a una prueba o a una medición realizada.
- [ ] 8.2 Actualizar `AGENTS.md` (estructura de proyectos, comandos de build, ejecución y empaquetado, reglas de dependencia y estado). Verificación: `dotnet format --verify-no-changes && dotnet build && dotnet test` en verde y los documentos coinciden con el código.
