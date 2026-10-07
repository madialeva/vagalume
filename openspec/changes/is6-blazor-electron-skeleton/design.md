## Context

Ver `proposal.md` — Why para la motivación. Lo relevante para el diseño es el estado de partida:

- Existe la solución con `Vagalume.Postgres.Embedded` (host de PostgreSQL embebido: `EmbeddedPostgresHost`, con estrategia por plataforma y colaboradores inyectados) y sus pruebas, que arrancan un PostgreSQL real con `TestCluster`. Los binarios se descargan en build a `artifacts/postgres/<plataforma>/`.
- El informe de hallazgos del change anterior deja riesgos abiertos que este change hereda: el servidor sobrevive al host por diseño (`pg_ctl`), por lo que hace falta una política de parada al salir; Windows x64 sigue sin validar (issue #2); la copia de seguridad es a nivel de ficheros.
- **ElectronNET.Core** está en la versión 0.6.0 (0.x, en modernización). Con *Core*, .NET y Electron se lanzan mutuamente; existe también la línea clásica `ElectronNET.API` 23.6.2, que se descarta por ser la que se está sustituyendo. Requiere Node.js 22 o posterior (en este entorno hay Node 24).
- `AGENTS.md` fija: orientación a objetos, inyección manual de dependencias con una única raíz de composición, las bibliotecas no dependen de un contenedor de DI (una aplicación puede usar el del host donde corre, solo en su raíz de composición), y no se añaden paquetes NuGet sin un change aprobado. Este change aprueba los que enumera `proposal.md` — Impact.
- La guía actual de Microsoft para Blazor apunta a .NET 11; el proyecto está fijado en **.NET 10** y se mantiene así.

## Goals / Non-Goals

**Goals:**

- Cerrar con evidencia la decisión de **Blazor Server** y la **arquitectura por capas** del código.
- Demostrar un recorrido completo (interfaz → `Core` → `Data` → PostgreSQL) que funcione igual en el host web y en el de escritorio.
- Medir el comportamiento real de Electron.NET Core: arranque, cierre, procesos huérfanos, tamaño y empaquetado.
- Demostrar que el puerto local del escritorio se puede proteger de otros procesos de la máquina.
- Dejar un informe de hallazgos honesto, incluyendo lo que no se ha podido comprobar.

**Non-Goals:**

- Cualquier parte de un dominio de facturación o contabilidad, o de los requisitos legales (Ley Antifraude, Verifactu).
- Autenticación, usuarios y roles en el host web (ASP.NET Core Identity). El host web del esqueleto no autentica y no debe exponerse a una red no confiable.
- Despliegue web real: Dockerfile, compose, proxy inverso con HTTPS, procedimiento de actualización y copias de seguridad del servidor.
- Abstracciones de funciones propias del escritorio (diálogos de fichero, menús, bandeja, impresión) detrás de interfaces con una implementación por host.
- Modo de escritorio «cliente de la instalación de empresa» (ventana de Electron apuntando a la URL de un servidor).
- Comparar o implementar alternativas de contenedor (Photino.Blazor, Avalonia, MAUI Blazor Hybrid). Este change decide Electron.NET.
- Soporte sin conexión para el modo web (exigiría WebAssembly y cambiaría la arquitectura).
- Firma de ejecutables, instalador con asistente, actualización automática y excepciones de antivirus.
- Soporte de macOS o de arquitecturas ARM.
- Impedir que se abran varias instancias de la aplicación de escritorio a la vez.
- Integración continua en GitHub y validación automática en Windows (issues #2 y #3).
- Actualización de versión mayor de PostgreSQL y almacenamiento seguro del secreto del clúster (issues #4 y #5).

## Decisions

### D1 — Blazor Web App con renderizado interactivo de servidor únicamente

La interfaz es una **Blazor Web App** con `AddInteractiveServerComponents` y el modo de renderizado `InteractiveServer` aplicado a toda la aplicación. No se usa WebAssembly ni el modo automático.

*Por qué*: en escritorio ya hay un proceso .NET local con acceso total al sistema; WebAssembly añadiría un segundo runtime dentro del navegador y una API REST hacia el propio proceso. En web, Blazor Server cubre el caso de decenas de usuarios por instalación autoalojada. La interfaz queda idéntica en los dos hosts y no hace falta API REST entre interfaz y lógica.

*Alternativas descartadas*: plantilla solo-Server clásica (la guía actual es la Blazor Web App y permite cambiar el modo por página más adelante); WebAssembly o automático (arranque más lento, segundo runtime, encierra el acceso a ficheros y base de datos tras una API).

### D2 — Capas con dependencias en un solo sentido, comprobadas automáticamente

```
Core  ← Data
Core  ← UI
Core, Data, UI ← Host.Web
Core, Data, UI, Postgres.Embedded, Electron.NET ← Host.Desktop
```

`Core` es .NET puro (entidades, reglas y las interfaces de repositorio que necesita). `Data` implementa esas interfaces con EF Core. `UI` es una Razor Class Library que solo conoce `Core`. Los hosts son la raíz de composición. Una prueba de arquitectura lee los ficheros de proyecto (referencias de proyecto y de paquete) y falla si se viola una regla.

*Por qué*: es la estructura de capas habitual en Blazor Server y la que permite cambiar de contenedor sin reescribir la interfaz. Comprobarla con una prueba evita que se degrade con el tiempo.

*Alternativa descartada*: un paquete de pruebas de arquitectura (NetArchTest u otro): añade una dependencia para algo que se resuelve leyendo XML.

### D3 — Raíz de composición en cada host, sin contenedor en las bibliotecas

Cada host tiene su propio `Program` y registra explícitamente los servicios con el contenedor de ASP.NET Core, solo en ese punto. `Core`, `Data` y `UI` exponen clases e interfaces normales y no dependen del contenedor. Tipos de EF Core como el `DbContext` se registran en el host.

*Por qué*: cumple `AGENTS.md`. La duplicación entre los dos `Program` es pequeña (unas líneas de registro) y es el precio de que ninguna biblioteca conozca el contenedor. Si crece, se extrae a un método de composición dentro de `Data`/`UI` sin depender del contenedor, en un change posterior.

### D4 — EF Core solo con PostgreSQL, modelo de notas y `xmin`

`Vagalume.Data` usa EF Core con el proveedor de Npgsql como única opción, sin abstraer el motor. El modelo es una entidad `Note` (identificador, texto, fecha de creación UTC) con el `xmin` de PostgreSQL como token de concurrencia optimista, mapeado a una propiedad de versión que `Core` trata como un valor opaco. Hay una única cadena de migraciones, en `Data`, aplicada al arrancar cada host con `MigrateAsync`. La herramienta `dotnet-ef` se fija en un manifiesto de herramientas local para que las migraciones sean reproducibles.

*Por qué*: es lo que justifica elegir PostgreSQL en los dos modos (un dialecto, una cadena de migraciones, `xmin` en lugar de un equivalente inexistente en SQLite). Un modelo de notas es lo mínimo que ejercita escritura, lectura y conflicto sin tocar el dominio de facturación.

*Alternativas descartadas*: Npgsql sin ORM (no cierra la decisión de EF Core, que este change quiere cerrar); abstraer el proveedor «por si acaso» (ceremonia que contradice la decisión de un solo motor).

*Consecuencia*: aplicar migraciones al arrancar es correcto con una instalación de una sola instancia. No se resuelve el arranque simultáneo de varias réplicas del host web ni un procedimiento de actualización del servidor (Non-Goals).

### D5 — Ciclo de vida del host de escritorio

Orden de arranque: comprobar que no se ejecuta como `root` (estrategia de plataforma ya existente) → arrancar el PostgreSQL embebido con `EmbeddedPostgresHost` en el directorio de datos del perfil del usuario (`Environment.SpecialFolder.LocalApplicationData`, que en Linux sigue `XDG_DATA_HOME`) → aplicar migraciones → arrancar Kestrel en `127.0.0.1` con puerto libre → abrir la ventana de Electron.NET Core. Al cerrar la ventana principal, el host detiene el servidor web y después `EmbeddedPostgresHost.StopAsync`.

El servidor web del escritorio se construye en una clase propia que no conoce Electron, de modo que se prueba con `WebApplicationFactory` sin lanzar Electron. Solo `Program` une esa clase con Electron.NET.

*Por qué*: resuelve la política de parada al salir que dejó abierta el change anterior, y deja lo sensible (token, migraciones, cierre) bajo pruebas automáticas.

*Cierre brusco*: si el host o Electron mueren de golpe, el servidor PostgreSQL puede quedar en marcha; el siguiente arranque lo reutiliza (comportamiento ya probado en el change anterior). Se comprueba también con la aplicación completa.

*Qué hace realmente Electron.NET Core* (cómo se lanzan mutuamente .NET y Electron, qué ocurre al matar uno de los dos, cómo se pasa la dirección a la ventana) se **verifica en una tarea propia antes de construir sobre ello**, porque la línea es 0.x y la documentación puede estar desactualizada. Los hallazgos van al informe.

### D6 — Token de sesión para el puerto local

El host de escritorio genera un token con un generador criptográfico en cada arranque y lo guarda solo en memoria. Un middleware, antes de cualquier otro, aplica estas reglas:

1. Rechaza (403, sin cuerpo) toda petición cuyo nombre de `Host` no sea `127.0.0.1`, para impedir el *DNS rebinding* (el puerto no aporta nada: un dominio atacante llevaría su propio nombre en la cabecera).
2. Acepta la petición si trae la cookie de sesión con el token (comparación en tiempo constante).
3. Si trae el token como parámetro de consulta, establece la cookie (`HttpOnly`, `SameSite=Strict`) y redirige a la misma ruta sin el parámetro.
4. En cualquier otro caso responde 403 sin cuerpo.

La ventana se abre con la dirección que lleva el token; el navegador conserva la cookie, que acompaña también al canal WebSocket de Blazor.

*Por qué*: hay que proteger el puerto local en una aplicación contable. Un token aleatorio por arranque es lo más simple que funciona con una ventana de Chromium.

*Alternativas descartadas*: canal que no sea TCP (socket Unix o canalización con nombre): Chromium no navega a ellos y Windows y Linux se comportarían distinto; prefijo de ruta aleatorio: el secreto acabaría en registros y en el historial; certificado de cliente: complejidad desproporcionada para un esqueleto.

*Alcance honesto de la protección*: frena a otros usuarios de la máquina y a procesos que solo escanean puertos. **No** protege frente a un proceso del mismo usuario que inspeccione la memoria o la línea de órdenes de Electron. Si Electron.NET Core pasa la dirección por la línea de órdenes, será visible para esos procesos; se comprueba y se registra en el informe.

### D7 — Empaquetado por plataforma con los binarios de PostgreSQL dentro

Se usa el empaquetado que ofrece Electron.NET Core (por debajo, el empaquetador de Electron) para generar un paquete autocontenido para `linux-x64` y otro para `win-x64`. Cada paquete copia dentro únicamente `artifacts/postgres/<plataforma>/`, obtenido con el script del change anterior para esa plataforma, y la aplicación localiza los binarios relativos a su propio directorio. El formato concreto (directorio, archivo comprimido, AppImage) se elige entre los que soporte la herramienta, priorizando el más sencillo, y se registra.

*Por qué*: el requisito es una instalación que no descargue nada en ejecución y no exija .NET ni Node.js al usuario. Incluir una sola plataforma por paquete mantiene el tamaño controlado.

*Limitación esperada*: solo se puede validar el paquete de Linux en este entorno. El de Windows se genera si la herramienta permite compilarlo desde Linux; si no, se documenta la configuración y queda **sin validar** (issue #2).

### D8 — Pruebas por capa, con PostgreSQL real

- `Core`: pruebas unitarias puras.
- `Data`: pruebas de integración contra un PostgreSQL real arrancado con el host embebido. El entorno de prueba de PostgreSQL (`TestEnvironment`, `TestCluster`) se extrae a un proyecto de apoyo compartido (`Vagalume.Testing`) para no duplicarlo.
- Hosts: pruebas con `WebApplicationFactory` contra un PostgreSQL real; el del escritorio prueba el middleware del token y el cierre sin lanzar Electron.
- Arquitectura: prueba que lee los `.csproj`.
- Electron y paquete: lo que necesita una ventana (apertura, cierre, paquete ejecutable) se verifica arrancando la aplicación empaquetada. Si hay un servidor X virtual disponible se automatiza; si no, se verifica a mano y se deja constancia del método en el informe.

*Por qué*: el valor de este change está en comprobar el comportamiento real, no en probar el cableado con dobles. Un dato falso sobre Electron sería peor que un dato honesto de «verificado a mano».

## Risks / Trade-offs

- **ElectronNET.Core es 0.x**: la API y el empaquetado pueden cambiar o tener fallos. → Una tarea inicial de verificación antes de construir sobre él, versión fijada y hallazgos en el informe. Si no es viable, es un resultado válido del spike y se documenta.
- **Peso**: el paquete lleva Chromium (decenas o cientos de MB) más 57–109 MiB de PostgreSQL. → Se mide y se registra; no se optimiza en este change.
- **Procesos huérfanos**: el servidor PostgreSQL y Electron pueden sobrevivir a un cierre brusco. → Parada ordenada al cerrar (D5), reutilización al arrancar y prueba del cierre brusco.
- **Entorno Linux sin pantalla**: puede no haber forma de abrir una ventana de Electron en este entorno. → Verificación manual por parte del usuario, con constancia en el informe. Linux también puede exigir el *sandbox* de Chromium (`chrome-sandbox`), que se documenta si resulta un obstáculo.
- **Windows sin validar**: toda la mitad de Windows (ventana, empaquetado, token, `tar`) queda sin comprobar. → Se marca explícitamente en el informe; sigue abierto el issue #2.
- **Token en la línea de órdenes**: ver D6, alcance honesto de la protección.
- **Host web sin autenticación**: cualquiera con acceso a la red usa la aplicación. → Documentado en la spec y en el README; Non-Goal con issue.
- **Dos instancias de escritorio a la vez**: compartirían el servidor PostgreSQL (soportado) pero habría dos ventanas y dos hosts sobre los mismos datos. → Fuera de alcance, se documenta.
- **Prototipo desechable**: el código no se escribe con calidad de producción, pero sí respeta las convenciones de `AGENTS.md`. Si el resultado es favorable, se revisa en un change propio.

## Open Questions

- Formato exacto del paquete de Linux (directorio, comprimido o AppImage): depende de lo que soporte Electron.NET Core y se decide al implementar sin cambiar specs ni tareas.
