## 1. Estructura de la solución

- [x] 1.1 Crear `global.json` fijando el SDK de .NET 10 y `Directory.Build.props` con la versión del programa (`0.1.0`), nulabilidad activada y advertencias como errores.
- [x] 1.2 Crear la solución `Vagalume.slnx` con la librería `src/Vagalume.Postgres.Embedded` y el proyecto de pruebas `tests/Vagalume.Postgres.Embedded.Tests` (xUnit).
- [x] 1.3 Crear `.editorconfig` y ampliar `.gitignore` con `bin/`, `obj/` y la carpeta de binarios descargados de PostgreSQL.

## 2. Adquisición de binarios

- [x] 2.1 Crear `eng/postgres-binaries.lock` con versión (18.6.0), URL de Maven Central y SHA-256 de los artefactos `linux-amd64` y `windows-amd64`.
- [x] 2.2 Escribir el script de descarga y verificación como aplicación de fichero único de .NET: descarga en build, comprueba la huella, extrae el `.jar` y el archivo interno a la carpeta de binarios de cada plataforma y falla si la huella no coincide.
- [x] 2.3 Inventariar el contenido real de cada paquete (ejecutables, bibliotecas, extensiones, proveedores de colación) y registrar el resultado para el informe.
- [x] 2.4 Integrar el script en el build del proyecto de pruebas para que se ejecute antes de las pruebas y solo descargue si falta o cambia la huella.

## 3. Host de PostgreSQL embebido

- [x] 3.1 Implementar la resolución del directorio de binarios por plataforma y el modelo de opciones (directorio de datos, usuario, base de datos).
- [x] 3.2 Implementar `initdb` en el primer arranque con codificación UTF-8, contraseña aleatoria por `--pwfile` y autenticación `scram-sha-256`.
- [x] 3.3 Generar `postgresql.conf` con `listen_addresses = '127.0.0.1'`, `unix_socket_directories` desactivado y el puerto libre elegido; persistir puerto y secreto con permisos solo del propietario.
- [x] 3.4 Implementar arranque, parada limpia y consulta de estado mediante `pg_ctl`, con reutilización de la instancia en marcha y detección de `postmaster.pid` obsoleto.
- [x] 3.5 Exponer la cadena de conexión para Npgsql y un error claro cuando el proceso se ejecuta como `root` en Linux.
- [x] 3.6 Implementar copia de seguridad a nivel de ficheros (parar, copiar el directorio de datos, rearrancar) y restauración hacia un directorio de datos nuevo.

## 4. Pruebas de integración

- [x] 4.1 Ciclo completo: arrancar, conectar con Npgsql, crear una tabla, escribir, leer y parar limpio.
- [x] 4.2 Acceso restringido: la conexión sin contraseña es rechazada, el servidor solo escucha en `127.0.0.1` y no existe socket Unix.
- [x] 4.3 Cierre brusco: matar el proceso con una señal forzada, volver a arrancar y comprobar que los datos confirmados siguen ahí y que no se crea un segundo servidor.
- [x] 4.4 Reutilización: un segundo host sobre el mismo directorio de datos reutiliza la instancia en marcha.
- [x] 4.5 Copia y restauración: copiar un clúster con datos, restaurarlo en un directorio nuevo y comparar el contenido.
- [x] 4.6 Verificación de binarios: una huella alterada en el fichero de bloqueo hace fallar la adquisición.
- [x] 4.7 Funcionamiento sin red: las pruebas anteriores pasan con los binarios ya extraídos y sin acceso a Maven Central.
- [x] 4.8 Medir tamaño de cada distribución y tiempos de `initdb` y de arranque en frío y en caliente para el informe.

## 5. Validación en Windows

- [ ] 5.1 Ejecutar las pruebas de la sección 4 en Windows x64 (máquina o runner), comprobando además si hacen falta las bibliotecas de Visual C++ en una instalación limpia.
- [x] 5.2 Si no se dispone de Windows durante el change, dejar constancia explícita en el informe de que la plataforma queda sin validar.

## 6. Informe y documentación

- [x] 6.1 Añadir al `README.md` cómo ejecutar el prototipo y las pruebas, y un informe de hallazgos: contenido del paquete (incluida la ausencia de `pg_dump`/`pg_restore`), colaciones disponibles, dependencias de Windows, tamaños, tiempos, resultado de cada prueba y riesgos abiertos (entre ellos la actualización de versión mayor).
- [x] 6.2 Incluir en el informe una recomendación fundada sobre si el enfoque es viable para producción, señalando lo que no se ha podido comprobar.
- [x] 6.3 Dejar anotado el aviso de licencia de PostgreSQL que debería acompañar a la redistribución.
- [x] 6.4 Actualizar `AGENTS.md` (estructura, comandos y estado) con la solución resultante.
