## ADDED Requirements

### Requirement: Los binarios de PostgreSQL se fijan y se verifican en tiempo de build

El sistema SHALL obtener los binarios de PostgreSQL de cada plataforma soportada en tiempo de build, desde un artefacto publicado cuya versión y huella SHA-256 constan en un fichero de bloqueo versionado. El build SHALL fallar si la huella de un artefacto descargado no coincide con la del fichero de bloqueo. La aplicación en ejecución SHALL NOT descargar ningún binario.

#### Scenario: Los binarios se adquieren y se verifican

- **WHEN** el build se ejecuta para una plataforma soportada y faltan los binarios
- **THEN** el sistema SHALL descargar el artefacto indicado en el fichero de bloqueo, verificar su huella y extraer únicamente los binarios de esa plataforma

#### Scenario: Un artefacto manipulado se rechaza

- **WHEN** la huella de un artefacto descargado difiere de la del fichero de bloqueo
- **THEN** el build SHALL fallar y SHALL NOT extraer el artefacto

#### Scenario: Sin descargas en ejecución

- **WHEN** el host arranca con los binarios ya presentes y sin red disponible
- **THEN** el host SHALL arrancar el servidor sin intentar ningún acceso a la red

### Requirement: El clúster se inicializa una sola vez con codificación fija

El host SHALL inicializar el clúster de base de datos con `initdb` únicamente en el primer arranque, con codificación UTF-8, y SHALL NOT reinicializar un directorio de datos existente en arranques posteriores.

#### Scenario: El primer arranque inicializa el clúster

- **WHEN** el host arranca sobre un directorio de datos vacío
- **THEN** el host SHALL inicializar un clúster con codificación UTF-8 y una contraseña generada, y SHALL arrancar el servidor

#### Scenario: Los arranques posteriores conservan los datos

- **WHEN** el host arranca sobre un directorio de datos que ya contiene un clúster
- **THEN** el host SHALL NOT ejecutar `initdb` de nuevo y los datos existentes SHALL permanecer sin cambios

### Requirement: El acceso al servidor queda restringido al usuario local

El host SHALL configurar el servidor para que escuche únicamente en `127.0.0.1`, en un puerto libre elegido al arrancar y persistido, con los sockets Unix desactivados, y SHALL exigir autenticación por contraseña `scram-sha-256` con una contraseña generada aleatoriamente. El servidor SHALL NOT usar autenticación `trust`. La contraseña generada SHALL guardarse en un fichero legible solo por su propietario.

#### Scenario: El servidor escucha solo en el bucle local

- **WHEN** el servidor está en marcha
- **THEN** SHALL aceptar conexiones en `127.0.0.1` y SHALL NOT aceptarlas en ninguna otra interfaz de red

#### Scenario: Una conexión sin contraseña se rechaza

- **WHEN** un cliente se conecta al servidor en marcha sin aportar la contraseña
- **THEN** el servidor SHALL rechazar la conexión

#### Scenario: Una conexión con la contraseña generada se acepta

- **WHEN** un cliente se conecta con la cadena de conexión que expone el host
- **THEN** el servidor SHALL aceptar la conexión

#### Scenario: El fichero del secreto es solo del propietario

- **WHEN** el host guarda la contraseña generada
- **THEN** el fichero SHALL ser legible únicamente por su propietario

### Requirement: El servidor se detiene de forma limpia

El host SHALL detener el servidor cuando se le solicite mediante una parada rápida, y SHALL dejar el directorio de datos en un estado que el siguiente arranque no necesite recuperar.

#### Scenario: Parada limpia

- **WHEN** se solicita al host detener un servidor en marcha
- **THEN** el proceso del servidor SHALL terminar y el host SHALL informar de que no hay ningún servidor en marcha para ese directorio de datos

### Requirement: El servidor se recupera tras una terminación brusca

Tras terminar forzosamente el proceso del servidor, el host SHALL arrancar un servidor operativo sobre el mismo directorio de datos en el siguiente arranque, SHALL conservar toda transacción confirmada antes de la terminación y SHALL NOT arrancar un segundo servidor sobre ese directorio de datos.

#### Scenario: Terminación forzosa y reinicio

- **WHEN** el proceso del servidor se termina forzosamente tras confirmar una fila y el host arranca de nuevo sobre el mismo directorio de datos
- **THEN** el servidor SHALL arrancar y la fila confirmada SHALL poder leerse

#### Scenario: Fichero de identificador de proceso obsoleto

- **WHEN** el directorio de datos contiene un `postmaster.pid` dejado por un servidor terminado
- **THEN** el host SHALL arrancar un servidor operativo y SHALL NOT fallar por culpa del fichero obsoleto

### Requirement: Una instancia en marcha se reutiliza

Cuando ya hay un servidor en marcha sobre el directorio de datos, el host SHALL reutilizarlo y SHALL NOT arrancar otro.

#### Scenario: Segundo host sobre el mismo directorio de datos

- **WHEN** un segundo host arranca sobre un directorio de datos cuyo servidor ya está en marcha
- **THEN** el segundo host SHALL reutilizar el servidor en marcha y el número de procesos de servidor para ese directorio de datos SHALL seguir siendo uno

### Requirement: Copia de seguridad y restauración

El host SHALL crear una copia de seguridad del clúster deteniendo el servidor y copiando su directorio de datos a nivel de ficheros, y SHALL restaurarla en un directorio de datos nuevo, conservando el contenido. El host SHALL NOT depender de `pg_dump` ni de `pg_restore`, que el paquete mínimo de binarios no incluye.

#### Scenario: La copia se restaura en un clúster nuevo

- **WHEN** se hace una copia de seguridad de un clúster con filas y se restaura en un directorio de datos nuevo
- **THEN** el servidor arrancado sobre el directorio restaurado SHALL contener las mismas filas que el original

#### Scenario: La copia se hace con el servidor parado

- **WHEN** se solicita una copia de seguridad con el servidor en marcha
- **THEN** el host SHALL detener el servidor antes de copiar y SHALL dejarlo de nuevo en marcha al terminar

### Requirement: La ejecución como root se rechaza con un error claro

En Linux, cuando el proceso del host se ejecuta como `root`, el host SHALL fallar antes de intentar arrancar el servidor e informar de un error que indique que el servidor no puede ejecutarse como `root`.

#### Scenario: Ejecución como root

- **WHEN** el usuario `root` arranca el host en Linux
- **THEN** el host SHALL informar de un error que indique que el servidor no puede ejecutarse como `root` y SHALL NOT arrancar el servidor

### Requirement: Se soportan Linux x64 y Windows x64

El host SHALL soportar Linux x64 y Windows x64. Cada distribución SHALL contener únicamente los binarios de su propia plataforma.

#### Scenario: La distribución contiene una sola plataforma

- **WHEN** se produce la distribución de una plataforma
- **THEN** SHALL contener los binarios de PostgreSQL de esa plataforma y SHALL NOT contener los de la otra
