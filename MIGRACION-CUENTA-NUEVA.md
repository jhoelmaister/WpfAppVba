# Puesta en marcha en una cuenta de GitHub nueva

> **Para Claude Code (o quien haga la migración) en la cuenta nueva.**
> Este repo se movió a una cuenta de GitHub distinta. Seguí este checklist para que
> la app **corra correctamente**, siga **conectando al VPS** y las **auto-actualizaciones**
> funcionen. Es self-contained: no necesitás contexto de conversaciones previas.

## Qué es este proyecto (30 segundos)

- **`SistemaGestion/`** — app de escritorio Windows (WPF, .NET 8): gestión de artículos,
  pedidos, facturas, traspasos, etc.
- **`VisorEmpresa/`** — app compañera (WPF, .NET 8) que ve la empresa completa. Tiene su
  propio workflow de release.
- **`ConexionBroker/`** — servicio ASP.NET Core (`net8.0`) que corre **en un VPS del dueño**,
  aparte de GitHub. Valida el login y le entrega a la app la conexión real a SQL Server.
- La app instalada se **auto-actualiza** con **Velopack**, leyendo las **Releases de ESTE repo**
  (por eso el repo debe ser **público**).

---

## 1. ⛔ Lo que NO hay que tocar

1. **El broker / VPS es independiente del repo.** La app se conecta a
   `https://conexion.jhoelmaister.tech` (constante `AuthBrokerClient.BrokerUrl`, en
   `SistemaGestion/AuthBrokerClient.cs` y `VisorEmpresa/AuthBrokerClient.cs`).
   **NO cambiar esa URL** al migrar el repo: el VPS sigue siendo el mismo y no depende de
   GitHub. La conexión al VPS "ya funciona" sola mientras esa constante quede igual.
2. **No hay credenciales en el repo.** Las credenciales reales de SQL Server viven **solo en
   el VPS** (el repo solo tiene placeholders `COMPLETAR_...` en `ConexionBroker/appsettings.json`).
   No inventar, no pedir, no commitear credenciales.

---

## 2. ✅ Lo que SÍ hay que cambiar: la URL del repo (para la auto-actualización)

La app busca sus actualizaciones en la URL de **este** repo, que está **fija en el código y en
los workflows** apuntando todavía a la cuenta vieja (`jhoelmaister/wpfappvba`). Hay que
reemplazarla por la URL de este repo nuevo.

**Detectá la URL nueva:**
```bash
git remote get-url origin
```

**Reemplazá** todas las ocurrencias de `https://github.com/jhoelmaister/wpfappvba`
(sin distinguir mayúsculas) por la URL de este repo en estos 4 archivos:

| Archivo | Qué hay |
|---|---|
| `SistemaGestion/ActualizadorApp.cs` | `const string RepoUrl = "..."` |
| `VisorEmpresa/ActualizadorApp.cs`   | `const string RepoUrl = "..."` |
| `.github/workflows/release.yml`     | 2 líneas `--repoUrl ...` |
| `.github/workflows/release-visor.yml` | 2 líneas `--repoUrl ...` |

> Podés confirmar que no quedó ninguna con:
> `git grep -niI "jhoelmaister/wpfappvba" -- '*.cs' '*.yml'` (debe dar vacío).

Después: **commit + push a `master`**.

**Opcional (docs):** varios `.md` mencionan la cuenta vieja (links "Run workflow", etc.). No
afectan el funcionamiento, pero conviene actualizarlos:
`git grep -nI "jhoelmaister/WpfAppVba" -- '*.md'`.

---

## 3. Requisitos para que la actualización funcione

- **El repo tiene que ser PÚBLICO** (Velopack lee las releases sin token; si es privado, se
  rompe la actualización).
- **Actions habilitadas** en el repo nuevo (el `GITHUB_TOKEN` del workflow es automático, no
  hace falta cargar ningún secret).
- **El número de versión solo SUBE, nunca se reinicia.** Está en `<Version>` de cada `.csproj`
  (`SistemaGestion/SistemaGestion.csproj` para la app principal; `VisorEmpresa/VisorEmpresa.csproj`
  para el visor). Si la última publicada fue `1.0.7`, la próxima es `1.0.8` — **nunca** volver a
  `1.0.0`, porque las apps no "actualizan hacia abajo".

---

## 4. Cómo se publica una actualización

1. Subir `<Version>` en el `.csproj` correspondiente (siguiente parche o minor).
2. Llevar ese cambio a `master`.
3. En GitHub → pestaña **Actions** → workflow **release.yml** (o **release-visor.yml** para el
   visor) → **Run workflow**. Eso compila y publica la Release (Setup.exe + `.nupkg` + `RELEASES`).
4. La app instalada detecta la versión nueva y muestra el botón **🔄 Actualizar** (manual/opt-in).

Detalle completo en `PUBLICAR-ACTUALIZACIONES.md` y `CREAR-NUEVA-VERSION.md`.

---

## 5. Migrar las apps YA instaladas (una sola vez)

Las apps instaladas antes de la mudanza buscan updates en la URL **vieja** (está compilada
adentro del binario). Para pasarlas al repo nuevo:

- **Pocas PCs (caso típico de negocio chico):** publicá una versión desde el repo nuevo (paso 4)
  y **reinstalá la app una vez** en cada PC con ese `Setup.exe` nuevo. Corte total, sin depender
  del repo viejo.
- **Muchas PCs / no se puede reinstalar:** hay que publicar una "versión puente" **desde el repo
  viejo** (ya con estas URLs nuevas) para que se auto-actualicen y queden apuntando al repo nuevo;
  recién después se puede retirar el repo viejo.

---

## 6. Build y verificación

- Es **WPF (`-windows`)**: se compila/prueba en **Windows con el SDK de .NET 8** (Visual Studio
  2022 o `dotnet`). El `ConexionBroker` es `net8.0` multiplataforma.
- **No se puede compilar en el entorno cloud de Claude Code** (sin SDK). Todo cambio se aplica
  siguiendo los patrones existentes, pero **verificá compilando localmente** antes de publicar.
- La auto-actualización **solo corre cuando la app está instalada vía Velopack** (no al correr
  desde `bin/` o Visual Studio): `ActualizadorApp.HayActualizacionAsync()` devuelve `false` si no
  está instalada. Para probar updates de verdad hay que instalarla.

---

## 7. Reglas del proyecto

Leé **`CLAUDE.md`** y **`CONTEXT.md`** (ya están en el repo) antes de tocar código: tienen las
reglas de trabajo, la arquitectura y el historial. En particular, la regla de Git del proyecto
(trabajar y pushear en `master`; las ramas `claude/*` no se pushean).

---

## Resumen ultra-corto

1. Repo nuevo **público** + Actions habilitadas.
2. **No tocar** `AuthBrokerClient.BrokerUrl` (el VPS es aparte y sigue igual).
3. Reemplazar la URL vieja del repo por la nueva en los **4 archivos** del punto 2 → push.
4. **No reiniciar** el número de versión.
5. Publicar con **Run workflow** y **reinstalar la app una vez** en cada PC.
