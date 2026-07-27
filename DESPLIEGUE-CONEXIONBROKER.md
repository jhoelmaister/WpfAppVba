# Despliegue de ConexionBroker — registro de lo hecho

Este documento registra el despliegue real del servicio `ConexionBroker/`
(ver también `ConexionBroker/README.md` para la guía genérica) sobre la
infraestructura actual de la empresa. Sirve como referencia para reinstalar,
diagnosticar o migrar el servicio más adelante.

## Qué es esto y por qué existe

Antes, `SistemaGestion`/`VisorEmpresa` guardaban la contraseña real de SQL
Server cifrada en cada PC (`%AppData%\SistemaGestion\conexion.dat`), y había
que cargarla a mano en cada instalación. Ahora esa contraseña vive **solo**
en el servidor, dentro de `ConexionBroker`. Las apps ya no guardan ni piden
ninguna configuración de conexión: al loguear, mandan usuario/contraseña a
`https://conexion.jhoelmaister.tech`, que valida contra la tabla `usuarios`
y devuelve la conexión real de SQL Server solo en memoria, para esa sesión.

La URL del broker queda **fija en el código** (`AuthBrokerClient.BrokerUrl`,
en `SistemaGestion/AuthBrokerClient.cs` y `VisorEmpresa/AuthBrokerClient.cs`)
— no hay pantalla de "elegir servidor" en la app.

## Infraestructura real

- **VPS**: `179.197.71.81`, Hostinger, Ubuntu 24.04.4 LTS. Mismo servidor
  donde ya corre **SQL Server 2022 para Linux**.
- **Acceso**: SSH como `root` (`ssh root@179.197.71.81`).
- **Dominio**: `jhoelmaister.tech`, gestionado en Hostinger (hPanel → DNS /
  Nameservers). El registro **A** de la raíz (`@`) ya apuntaba a esta misma
  IP de antes (otro uso). Se agregó un registro nuevo, aparte:
  - Tipo `A`, Nombre `conexion`, Contenido `179.197.71.81`, TTL `300`.
  - Resultado: `conexion.jhoelmaister.tech` → `179.197.71.81`.
- **Código del broker**: clonado directo del repo en el propio VPS, en
  `~/WpfAppVba` (o sea `/root/WpfAppVba/ConexionBroker`).
- **Publicado en**: `/opt/conexionbroker` (salida de `dotnet publish`).
- **Credenciales reales de SQL Server**: en
  `/opt/conexionbroker/appsettings.Production.json` (y también quedó una
  copia en la carpeta fuente `~/WpfAppVba/ConexionBroker/`, que es de donde
  `dotnet publish` la copia automáticamente al publicar de nuevo). **Este
  archivo no está en git** — si se reinstala el servidor, hay que crearlo de
  nuevo a mano (ver plantilla en `ConexionBroker/README.md`, sección 3).
- **Servicio del broker**: `systemd`, unidad `conexionbroker.service`
  (`/etc/systemd/system/conexionbroker.service`), escuchando en
  `http://127.0.0.1:5080` (solo local, no expuesto directo a internet).
- **HTTPS**: `Caddy` (instalado vía `apt`, repo oficial de Caddy), como
  servicio systemd (`caddy.service`), con `/etc/caddy/Caddyfile`:
  ```
  conexion.jhoelmaister.tech {
      reverse_proxy 127.0.0.1:5080
  }
  ```
  Caddy consigue y renueva el certificado Let's Encrypt solo.
- **Firewall**: `ufw` está **inactivo** en este VPS — no hace falta abrir
  puertos a nivel de sistema operativo para que esto funcione.

## Cómo verificar que está andando

Desde el VPS (SSH):
```bash
systemctl status conexionbroker
systemctl status caddy
curl -i http://127.0.0.1:5080/ping     # tiene que dar 200 OK
```

Desde cualquier PC (navegador): `https://conexion.jhoelmaister.tech/ping` —
tiene que mostrar una página en blanco, sin advertencia de certificado.

Si `/ping` da `503`, el motivo real queda en el log del servicio:
```bash
journalctl -u conexionbroker -n 50 --no-pager
```

## Cómo actualizar el broker cuando cambia el código

```bash
cd ~/WpfAppVba
git pull origin master
cd ConexionBroker
dotnet publish -c Release -o /opt/conexionbroker
systemctl restart conexionbroker
```

(`appsettings.Production.json`, si sigue estando en la carpeta fuente, se
vuelve a copiar solo al publicar. Si no está, copiarlo a mano a
`/opt/conexionbroker/` antes del `restart`.)

## Problemas reales que aparecieron durante este despliegue (ya corregidos en el código)

1. **`PasswordHasher` no reconocido al compilar** (`CS0103` x3) — faltaba un
   `using ConexionBroker;` en `Program.cs`. Corregido.
2. **`appsettings.Production.json` en la carpeta equivocada** — se creó por
   error en `/root/` en vez de `/root/WpfAppVba/ConexionBroker/`. El archivo
   tiene que estar en la misma carpeta que `Program.cs` (o, tras publicar, en
   la misma carpeta que `ConexionBroker.dll`).
3. **`Globalization Invariant Mode is not supported`** — el `.csproj` tenía
   `<InvariantGlobalization>true</InvariantGlobalization>`, que rompe
   `Microsoft.Data.SqlClient`. Se quitó esa línea del `.csproj` (commit
   `b813cc7`). **No volver a agregarla.**
4. **Puerto 5080 ocupado** (`Address already in use`) al reiniciar pruebas —
   quedó un proceso viejo de una sesión SSH cortada. Se resolvió con
   `fuser -k 5080/tcp` antes de volver a levantar el servicio (con
   `systemd` administrando el proceso esto ya no debería pasar en uso
   normal).

## Si el dominio o el VPS cambian alguna vez

1. Actualizar el registro DNS (o migrar todo el broker al VPS nuevo,
   repitiendo los pasos de `ConexionBroker/README.md`).
2. Editar la constante `BrokerUrl` en `SistemaGestion/AuthBrokerClient.cs` y
   `VisorEmpresa/AuthBrokerClient.cs` con la nueva URL.
3. Publicar una versión nueva de la app (ver `PUBLICAR-ACTUALIZACIONES.md`)
   para que les llegue a todos los usuarios.
