# Publicar actualizaciones (Velopack)

Guía del flujo de release de la app con **Velopack**. Reemplaza al paso de Inno Setup
para la distribución con auto-actualización.

## ⭐ Forma recomendada: publicar desde cualquier sesión (GitHub Actions)

GitHub compila la app en un **Windows en la nube** (workflow `.github/workflows/release.yml`),
así que **no necesitas tu PC ni un token manual**. Funciona incluso desde Claude en la web.

**Flujo (lo que haces tú o le pides a Claude):**

1. Haz tus cambios de código.
2. Sube el número en `WpfAppVba/WpfAppVba.csproj` → `<Version>` (ej. `1.0.0` → `1.0.1`).
3. Crea y empuja un tag con esa versión:

   ```bash
   git commit -am "v1.0.1: descripción del cambio"
   git tag v1.0.1
   git push origin v1.0.1
   ```

4. GitHub Actions hace solo el resto: `publish` → `vpk pack` → publica la **Release**.
   (Lo ves en la pestaña **Actions** del repo.)
5. Las apps ya instaladas mostrarán **🔄 Actualizar** la próxima vez que se abran.

> También puedes lanzarlo a mano sin tag: pestaña **Actions** → *Publicar release* →
> *Run workflow* → escribe la versión.

> En una sesión de Claude basta con: *"publica la versión 1.0.1"*. Claude sube el
> `<Version>`, hace commit y empuja el tag; GitHub compila y publica.

---

Lo de abajo es el camino **manual** (publicar desde tu propia PC Windows), por si lo
necesitas. Con GitHub Actions normalmente no hace falta.

## Requisitos (una sola vez)

```powershell
# Instalar la CLI de Velopack como herramienta global de .NET
dotnet tool install -g vpk
```

El feed de actualización es el repo **público** `https://github.com/jhoelmaister/wpfappvba`
(configurado en `WpfAppVba/ActualizadorApp.cs`). Para **subir** releases necesitas un
token de GitHub con permiso sobre ese repo.

### Crear el token de GitHub (una sola vez)

Dos tipos de token sirven. El **Fine-grained** es el recomendado (permisos mínimos).

**Opción A — Fine-grained token (recomendado):**

1. Entra a <https://github.com/settings/tokens?type=beta> (o: foto de perfil →
   *Settings* → *Developer settings* → *Personal access tokens* → *Fine-grained tokens*).
2. *Generate new token*.
3. **Token name**: algo como `publicar-wpfappvba`.
4. **Expiration**: elige una fecha (ej. 90 días o *No expiration* si lo prefieres).
5. **Repository access** → *Only select repositories* → marca **`wpfappvba`**.
6. **Permissions** → *Repository permissions* → busca **Contents** → ponlo en
   **Read and write**. (Eso basta para crear releases y subir los archivos.)
7. *Generate token* y **copia el valor** (empieza por `github_pat_…`). Solo se muestra
   una vez.

**Opción B — Token clásico (más simple, más permisos):**

1. Entra a <https://github.com/settings/tokens> → *Generate new token (classic)*.
2. **Note**: `publicar-wpfappvba`; elige *Expiration*.
3. Marca el scope **`repo`** (completo).
4. *Generate token* y copia el valor (empieza por `ghp_…`).

### Usar el token

Pásalo como variable de entorno (no lo escribas en sitios compartidos ni lo subas al repo):

```powershell
$env:GITHUB_TOKEN = "PEGA_AQUÍ_TU_TOKEN"
```

> 🔒 **Seguridad:** trata el token como una contraseña. Si se filtra (lo pegas en un
> chat, captura, commit…), revócalo en la misma página de *Settings → tokens* y crea
> otro. El token **no** va dentro de la app ni del repo: solo lo usas tú al publicar.

## Publicar una versión nueva — forma rápida (un solo comando)

Usa el script `publicar.ps1` (raíz del repo). Lee la versión sola del csproj y hace
los 3 pasos (publish → pack → upload):

```powershell
$env:GITHUB_TOKEN = "ghp_xxxxxxxxxxxxxxxx"
# 1) Sube <Version> en WpfAppVba\WpfAppVba.csproj (ej. 1.0.0 -> 1.0.1)
# 2) Ejecuta:
.\publicar.ps1
```

Si quieres forzar una versión sin tocar el csproj: `.\publicar.ps1 -Version 1.2.0`.

El resto de esta guía explica los mismos 3 pasos a mano (por si quieres entenderlos
o ajustarlos).

## Publicar una versión nueva — paso a paso (manual)

1. **Sube el número de versión** en `WpfAppVba/WpfAppVba.csproj` (`<Version>`).
   Ej.: `1.0.0` → `1.0.1`. Velopack solo ofrece la actualización si la release es mayor
   que la instalada.

2. **Publica** la app como ya lo haces (self-contained single-file):

   ```powershell
   dotnet publish WpfAppVba/WpfAppVba.csproj -c Release -r win-x64 --self-contained true `
       -p:PublishSingleFile=true -p:DebugType=none `
       -o .\publish
   ```

3. **Empaqueta** con Velopack (genera Setup.exe + .nupkg full + delta + RELEASES):

   ```powershell
   vpk pack `
       --packId SistemaGestion `
       --packVersion 1.0.1 `
       --packDir .\publish `
       --mainExe WpfAppVba.exe `
       --packTitle "Sistema de Gestión" `
       --icon WpfAppVba\icono.ico
   ```

   > `--packVersion` debe coincidir con `<Version>` del csproj.
   > El resultado queda en `.\Releases`.

4. **Sube** las releases al repo público de GitHub:

   ```powershell
   vpk upload github `
       --repoUrl https://github.com/jhoelmaister/wpfappvba `
       --publish `
       --releaseName "v1.0.1" `
       --tag v1.0.1 `
       --token $env:GITHUB_TOKEN
   ```

Listo. Los usuarios con la app instalada verán el botón **🔄 Actualizar** en la barra
superior la próxima vez que la abran.

## Primera distribución (instalador inicial)

La **primera vez**, el usuario instala con el `Setup.exe` que genera `vpk pack`
(en `.\Releases\SistemaGestion-win-Setup.exe`). Se instala **por-usuario** en
`%LocalAppData%\SistemaGestion` (sin permisos de administrador, sin UAC).
A partir de ahí, todas las actualizaciones son in-app.

## Cómo se ve para el usuario

1. Al abrir la app, en segundo plano se consulta si hay versión nueva (no molesta).
2. Si la hay, aparece **🔄 Actualizar** en la barra superior. Es **opcional**.
3. Al pulsarlo, descarga en segundo plano con **barra de progreso** (sigue usando la app).
4. Al terminar, el botón cambia a **✅ Reiniciar para actualizar**.
5. El usuario reinicia **cuando quiera** y la app vuelve a abrir ya actualizada.

## Notas

- En **desarrollo** (ejecutando desde Visual Studio o `bin\`), el botón nunca aparece:
  Velopack solo actúa cuando la app está instalada vía su `Setup.exe`.
- Las actualizaciones son **delta** (solo se descarga lo que cambió entre versiones),
  por eso son rápidas aunque el ejecutable completo pese ~160 MB.
- Firmar el `Setup.exe` con un certificado de código es **opcional** pero recomendable
  para evitar el aviso de SmartScreen de Windows (`vpk pack --signParams ...`).
