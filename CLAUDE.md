# CLAUDE.md — WpfAppVba-sistemaControl

## Reglas obligatorias de cada sesión

1. Contexto primero
   - Leer CONTEXT.md y el código existente relevante antes de proponer cualquier cambio.
   - No reinventar patrones ya establecidos (estilos, nombres, estructura del design system).

2. Plan antes de ejecutar
   - Mostrar un resumen del plan (archivos, cambios, motivo).
   - Esperar confirmación explícita del usuario antes de aplicar nada.

3. Ante la duda
   - Preguntar antes de asumir, especialmente en cambios estructurales o que afecten varios archivos.

4. CONTEXT.md
   - NO actualizar CONTEXT.md automáticamente después de cada cambio confirmado.
   - El usuario actualiza CONTEXT.md manualmente al finalizar la sesión.

Estas reglas aplican durante toda la sesión, no solo al inicio.

## Reglas de Git

- NUNCA hacer push de ramas `claude/*` al remoto.
- Solo hacer push de `master`.
- Las ramas `claude/*` existen únicamente en local.

> **Excepción inevitable — sesiones remotas/web (Claude Code on the web):** la plataforma
> obliga a desarrollar y pushear a una rama `claude/*` designada por la sesión; el
> contenedor es efímero y ese push es el único respaldo del trabajo hasta el merge final
> a `master`. Esa instrucción de plataforma no es opcional y no puede desactivarse desde
> este archivo. En esas sesiones, la regla de arriba se cumple en su intención (no usar
> `claude/*` como destino final, fusionar y pushear siempre a `master`), pero el push
> intermedio a la rama de sesión sí va a ocurrir.

## Publicar versiones (actualizaciones automáticas)

Este proyecto se distribuye con **Velopack** y publica releases vía **GitHub Actions**.
La app instalada detecta versiones nuevas y muestra un botón **🔄 Actualizar** (manual/opt-in).

**Cuando el usuario pida "sube/saca/publica la versión X.Y.Z" (o similar):**

0. **Valida el número ANTES de aplicarlo** (lee la versión actual del csproj y compara):
   - **Formato**: debe ser `X.Y.Z` (tres números). Si el usuario da algo incompleto como
     `1.1`, NO asumas: pregúntale si quiere `1.1.0` o `1.0.2`.
   - **Debe ser MAYOR** que la actual (semver). Si es igual o menor, NO la apliques: avísale
     (ej. "ya estás en 1.0.1, no puedo bajar/repetir").
   - **El salto debe ser razonable**: lo normal es el siguiente parche (1.0.1 → 1.0.2),
     el siguiente minor (1.0.1 → 1.1.0) o el siguiente major (1.0.1 → 2.0.0). Si el número
     pedido salta de más (ej. 1.0.1 → 1.5.0, o 1.0.1 → 3.0.0), CONFIRMA con el usuario antes
     de aplicarlo, por si fue un error de tecleo.
   - Ante cualquier duda con el número, pregunta; nunca apliques un número raro en silencio.
1. Edita `<Version>` en `WpfAppVba/WpfAppVba.csproj` con el número indicado.
2. Lleva ese cambio a la rama `master` (commit + PR + merge; el bump DEBE quedar en `master`,
   no solo en la rama de trabajo).
3. Avisa al usuario que ya puede lanzar el release con **Run workflow**:
   https://github.com/jhoelmaister/WpfAppVba/actions/workflows/release.yml
   (el paso de "Run workflow" lo hace el usuario; Claude en la web no puede dispararlo
   ni empujar tags).

**Notas clave:**
- El número de versión es la única fuente de verdad: súbelo siempre (1.0.0 → 1.0.1 → …).
- El feed de releases es el propio repo (debe estar **público** para que la app lea las
  releases sin token).
- Detalles completos del flujo en `PUBLICAR-ACTUALIZACIONES.md`.
