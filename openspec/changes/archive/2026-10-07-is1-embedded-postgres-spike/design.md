## Context

Ver `proposal.md` — Why para la motivación. Lo relevante para el diseño es el estado de partida y las piezas disponibles:

- El repositorio parte vacío: no existe ningún código.
- Zonky publica en Maven Central artefactos `embedded-postgres-binaries-<plataforma>` por cada versión de PostgreSQL. Cada artefacto es un `.jar` (un fichero zip) que contiene un archivo comprimido con el juego mínimo de binarios. A fecha de este change existen artefactos para `linux-amd64` y `windows-amd64`, con PostgreSQL 16, 17 y 18; la última versión publicada es la 18.6.0.
- PostgreSQL se licencia bajo la licencia PostgreSQL, permisiva, que permite redistribuir los binarios con la aplicación conservando el aviso de copyright.
- PostgreSQL **se niega a ejecutarse como `root`** en Linux y fija su directorio de datos en el primer `initdb`: la codificación y la colación elegidas ahí son muy difíciles de cambiar después.
- Existen paquetes NuGet que envuelven un PostgreSQL embebido, pero son jóvenes, de un único mantenedor y no cubren Linux de serie. Se ha decidido **no depender de ellos** y escribir el host propio, que es pequeño y permite controlar el comportamiento en las dos plataformas.

## Goals / Non-Goals

**Goals:**

- Demostrar con pruebas automatizadas que un PostgreSQL empaquetado puede gestionarse de forma fiable desde .NET 10 en Linux x64 y Windows x64.
- Validar los cinco riesgos concretos: contenido del paquete mínimo, recuperación tras cierre brusco, acceso restringido, copia de seguridad y restauración, y funcionamiento sin red.
- Dejar un informe de hallazgos honesto, incluyendo lo que no se ha podido comprobar.

**Non-Goals:**

- Decidir si algún producto adopta .NET, Blazor o PostgreSQL embebido. Este change solo aporta evidencia.
- Construir interfaz, EF Core, migraciones de esquema de negocio o cualquier parte de un dominio de facturación.
- Instalador, firma de ejecutables o excepciones de antivirus.
- Soporte de macOS o de arquitecturas ARM.
- Actualización entre versiones mayores de PostgreSQL (`pg_upgrade` o dump/restore). Se documenta como riesgo conocido, no se implementa.
- Almacenamiento seguro de la contraseña con los mecanismos del sistema (DPAPI, llavero del sistema operativo). El prototipo usa un fichero protegido por permisos del propietario.
- Integración continua en GitHub, y con ella la validación automática en Windows. Se define en un change posterior.

## Decisions

### D1 — Solución .NET con el SDK fijado

La solución vive en la raíz con `global.json` fijando el SDK de .NET 10, `Directory.Build.props` con la versión del programa y las opciones comunes (nulabilidad activada, advertencias como errores) y `.editorconfig` para el formato.

*Por qué*: un repositorio nuevo permite establecer desde el principio una base estricta y reproducible, en lugar de endurecerla después sobre código ya escrito.

### D2 — Binarios descargados en build, fijados por versión y huella

Un script de build descarga el `.jar` de cada plataforma desde Maven Central, verifica su SHA-256 contra un fichero de bloqueo versionado (`eng/postgres-binaries.lock`, con versión, URL y huella por plataforma) y extrae el archivo interno a una carpeta de binarios ignorada por git. Si la huella no coincide, el build falla. La aplicación **nunca** descarga nada en ejecución. El script se escribe como aplicación de fichero único de .NET 10 para que funcione igual en Linux y en Windows, sin duplicarlo en Bash y PowerShell.

*Por qué*: una instalación sin conexión o tras un cortafuegos estricto no puede depender de una descarga en el primer arranque, y fijar la huella protege de una manipulación del artefacto publicado. Actualizar PostgreSQL pasa a ser un cambio explícito y revisable del fichero de bloqueo.

*Alternativas descartadas*:
- **Descarga en el primer arranque** (lo que hacen otras librerías por defecto): falla sin red y amplía la superficie de ataque.
- **Versionar los binarios en el repositorio**: engorda el historial de git con ficheros de decenas de megabytes por cada actualización.

### D3 — `pg_ctl` para el ciclo de vida, no lanzar `postgres` directamente

El host usa `initdb` una vez y después `pg_ctl start -w`, `pg_ctl stop -m fast -w` y `pg_ctl status`.

*Por qué*: `pg_ctl` ya sabe leer y validar `postmaster.pid`, esperar a que el servidor acepte conexiones y manejar las particularidades de Windows (token restringido, servicios). Reimplementarlo sobre un `Process` propio duplicaría esa lógica y reabriría errores ya resueltos. Además permite que la instancia **sobreviva a un cierre brusco del host** y se recupere o se reutilice en el siguiente arranque, que es precisamente lo que se quiere probar.

*Consecuencia*: el host no es dueño del proceso hijo. Debe consultar `pg_ctl status` antes de arrancar para no lanzar un segundo servidor sobre el mismo directorio de datos (D5).

### D4 — Acceso restringido con `scram-sha-256` y sin socket Unix

El clúster se inicializa con `--auth-local=scram-sha-256 --auth-host=scram-sha-256` y una contraseña aleatoria pasada por `--pwfile`. La configuración fija `listen_addresses = '127.0.0.1'`, `unix_socket_directories = ''` (desactivado) y un puerto libre elegido al arrancar y guardado junto al directorio de datos.

*Por qué*: escuchar en `127.0.0.1` evita el acceso desde la red, pero cualquier otro proceso de la máquina podría conectarse. Con `trust` (el valor por defecto de muchas librerías) bastaría eso. La contraseña obliga a conocer un secreto que solo está en el perfil del usuario. Se desactiva el socket Unix para que Linux y Windows se comporten igual y haya una sola superficie de acceso que revisar: es la opción más homogénea.

*Alternativa descartada*: socket Unix con permisos del propietario en Linux. Es más estricto, pero Windows no lo soporta, de modo que habría dos caminos distintos de probar.

*Almacén del secreto*: un fichero junto al directorio de datos, con permisos solo del propietario (`0600` en Linux, ACL heredada del perfil del usuario en Windows). Es una concesión explícita del prototipo; un producto real usaría el mecanismo del sistema operativo.

### D5 — Reutilización de instancia y recuperación

Al arrancar, el host ejecuta `pg_ctl status` sobre el directorio de datos:

- Si hay un servidor en marcha, lo **reutiliza** (lee el puerto guardado) y no lanza otro.
- Si no, pero queda un `postmaster.pid` obsoleto, deja que PostgreSQL lo detecte y recupere (`pg_ctl start` ya lo gestiona); el host solo informa del resultado.
- Si no hay nada, arranca uno nuevo.

*Por qué*: tras un `kill -9` del host o un corte de luz, el siguiente arranque no puede pelearse con restos del anterior. PostgreSQL recupera su propio WAL sin ayuda, así que el trabajo del host es no estorbar.

### D6 — Codificación y colación fijadas en `initdb`

El clúster se crea con codificación `UTF8`. La colación se decide con lo que realmente soporten los binarios mínimos: el prototipo comprueba qué proveedores y localizaciones incluyen (por ejemplo ICU o `C.UTF-8`) y deja el hallazgo documentado en el informe.

*Por qué*: es la decisión más cara de revertir y la que más depende de lo que contenga el paquete. No se asume nada antes de medirlo.

### D7 — Pruebas de integración reales, sin simular

Las pruebas arrancan un PostgreSQL de verdad en un directorio temporal, con un puerto libre. Matar el proceso se hace con una señal real (`kill -9` en Linux, terminación forzada en Windows). No se usan dobles de prueba para `pg_ctl`.

*Por qué*: lo que se quiere validar es el comportamiento del proceso real. Un doble solo probaría el código propio, no los riesgos.

### D8 — Copia de seguridad a nivel de ficheros

Como el paquete mínimo no incluye `pg_dump` ni `pg_restore`, la copia de seguridad detiene el servidor con `pg_ctl stop -m fast`, copia el directorio de datos completo y vuelve a arrancarlo. La restauración copia ese directorio a uno nuevo y arranca un servidor sobre él.

*Por qué*: es consistente sin herramientas adicionales y evita añadir una segunda fuente de binarios. *Limitaciones*: exige parar el servidor, la copia solo vale para la misma versión mayor y plataforma, y no permite migrar entre versiones mayores. Estas limitaciones se documentan en el informe.

*Alternativa descartada*: descargar los binarios oficiales solo para `pg_dump`/`pg_restore`, que añadiría otra fuente que fijar y verificar y mucho más peso.

## Risks / Trade-offs

- **Cobertura de Windows**: el entorno de desarrollo habitual es Linux. Sin una máquina o un runner de Windows, la mitad de la matriz queda sin validar. → La validación en Windows se marca explícitamente como pendiente en el informe si no se completa.
- **Contenido del paquete mínimo**: puede no incluir `pg_dump`, `pg_restore` o alguna extensión necesaria. → Comprobado: el paquete 18.6.0 solo incluye `initdb`, `pg_ctl` y `postgres`; no trae `pg_dump`, `pg_restore` ni `psql`. La copia de seguridad se resuelve a nivel de ficheros (D8) y la ausencia de volcado lógico se registra en el informe como limitación.
- **Dependencias de runtime en Windows**: los binarios pueden requerir las bibliotecas de Visual C++. → Se comprueba en una instalación limpia y se documenta.
- **Procesos huérfanos**: un servidor que sobrevive al host puede retener el puerto o los ficheros. → Cubierto por D5 y por una prueba específica.
- **Actualización de versión mayor**: queda fuera de alcance y es el mayor coste de mantenimiento a largo plazo de este enfoque. → Se registra en el informe como riesgo abierto.
- **Prototipo desechable**: el código no se escribe con calidad de producción. → Se documenta; si el resultado es favorable, el código se reescribe o se revisa en un change propio.

## Open Questions

Ninguna por ahora. La integración continua y la validación automática en Windows se tratan en un change posterior (ver Non-Goals).
