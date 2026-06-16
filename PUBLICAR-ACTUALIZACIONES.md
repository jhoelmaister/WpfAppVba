# Publicar actualizaciones (Velopack)

Guía del flujo de release de la app con **Velopack**. Reemplaza al paso de Inno Setup
para la distribución con auto-actualización.

## Requisitos (una sola vez)

```powershell
# Instalar la CLI de Velopack como herramienta global de .NET
dotnet tool install -g vpk
```

El feed de actualización es el repo **público** `https://github.com/jhoelmaister/wpfappvba`
(configurado en `WpfAppVba/ActualizadorApp.cs`). Para subir releases necesitas un token
de GitHub con permiso sobre ese repo, expuesto como variable de entorno:

```powershell
$env:GITHUB_TOKEN = "ghp_xxxxxxxxxxxxxxxx"
```

## Publicar una versión nueva (cada release)

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
