# ConexionBroker

Servicio mínimo (ASP.NET Core, .NET 8) que reemplaza el flujo anterior de
login: antes, `SistemaGestion`/`VisorEmpresa` guardaban la cadena de conexión
real de SQL Server (servidor, usuario, contraseña) cifrada en
`%AppData%\SistemaGestion\conexion.dat` en cada PC, y había que cargarla a
mano la primera vez en cada máquina.

Ahora **la contraseña real de SQL Server vive únicamente acá, en el
servidor** (nunca en las PCs de los empleados). El flujo pasa a ser:

1. El empleado abre la app y pone su usuario/contraseña de siempre (los
   mismos de la tabla `usuarios`).
2. La app le manda esas credenciales a este servicio por HTTPS.
3. Este servicio valida contra `usuarios` (mismo hash PBKDF2 que ya usaba
   `AppLoader.ValidarLogin`) y, si son correctas, le devuelve a la app la
   conexión real de SQL Server — **solo para esa sesión, en memoria**. La app
   nunca la vuelve a escribir a disco.
4. Al cerrar la app se pierde; la próxima vez se vuelve a pedir con el mismo
   login.

Una PC nueva solo necesita saber la URL de este servicio (no es secreta, se
puede compartir sin problema) — vos ya no tenés que tipear ninguna
contraseña de SQL Server en ningún lado.

## 1. Configurar las credenciales reales (nunca en el repo)

`appsettings.json` en este repo **solo tiene placeholders** — es público en
GitHub. Las credenciales reales van en uno de estos dos lugares (ninguno se
commitea, ver `.gitignore`):

**Opción A — archivo local en el servidor** (crear
`ConexionBroker/appsettings.Production.json` junto al ejecutable publicado):

```json
{
  "Sql": {
    "Servidor": "TU_SERVIDOR_SQL",
    "BaseDatos": "TU_BASE_DE_DATOS",
    "Usuario": "TU_USUARIO_SQL",
    "Contrasena": "TU_CONTRASEÑA_SQL"
  }
}
```

**Opción B — variables de entorno** (equivalentes, útil si preferís no tener
ni siquiera ese archivo en el disco):

```
Sql__Servidor=TU_SERVIDOR_SQL
Sql__BaseDatos=TU_BASE_DE_DATOS
Sql__Usuario=TU_USUARIO_SQL
Sql__Contrasena=TU_CONTRASEÑA_SQL
```

Usá un login de SQL Server con los permisos mínimos que la app necesita
(no el `sa`), si es posible.

## 2. Publicar

Desde el servidor (o compilando en otra máquina y copiando la carpeta):

```
dotnet publish ConexionBroker -c Release -r win-x64 --self-contained false -o C:\ConexionBroker
```

Copiá también `appsettings.Production.json` (opción A) a `C:\ConexionBroker`.

## 3. Correrlo como servicio de Windows

Para que arranque solo con el servidor:

```
sc create ConexionBroker binPath= "C:\ConexionBroker\ConexionBroker.exe --urls http://127.0.0.1:5080" start= auto
sc start ConexionBroker
```

(`--urls http://127.0.0.1:5080` lo deja escuchando SOLO en localhost, en
HTTP plano — el HTTPS lo pone el reverse proxy del paso 4. Así el proceso de
.NET no maneja certificados directamente.)

## 4. HTTPS obligatorio de cara a internet

Las credenciales viajan por acá — **nunca lo expongas en HTTP plano hacia
afuera**. Como ya tenés Windows Server, la forma más simple es un reverse
proxy delante de `127.0.0.1:5080`:

- **IIS** (ya viene con Windows Server): creá un sitio con enlace HTTPS
  (certificado propio, de tu CA, o gratuito vía `win-acme` para Let's
  Encrypt) y usá el módulo *Application Request Routing* (ARR) + *URL
  Rewrite* para reenviar todo a `http://127.0.0.1:5080`.
- **Caddy** (alternativa más simple, un solo binario): resuelve HTTPS
  automático solo con apuntar tu dominio; como reverse proxy hacia
  `127.0.0.1:5080` en 2 líneas de `Caddyfile`.

Elegí la que te resulte más cómoda de mantener — el resultado que necesita
la app es una URL `https://...` que responda en `/ping` y `/login`.

## 5. Apuntar la app ahí

En `SistemaGestion`/`VisorEmpresa`, desde el login → "Configurar conexión" →
Agregar → poné la URL pública HTTPS de este servicio (ej.
`https://conexion.tuempresa.com`) → "Probar conexión" → Guardar. A partir de
ahí, cualquier PC nueva solo necesita esa misma URL (no secreta) y el
usuario/contraseña normal de cada empleado.

## Notas

- La migración automática de contraseñas antiguas en texto plano (si
  `usuarios.llave` no tiene el formato de hash pero coincide) se conserva
  igual que en `AppLoader.ValidarLogin` — queda re-hasheada tras el primer
  login exitoso por acá.
- Este servicio es un buen lugar para agregar, más adelante, un límite de
  intentos fallidos de login (hoy no lo tiene) — todos los logins ya pasan
  por un único punto.
