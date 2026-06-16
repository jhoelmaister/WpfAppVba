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

## Publicar versiones (actualizaciones automáticas)

Este proyecto se distribuye con **Velopack** y publica releases vía **GitHub Actions**.
La app instalada detecta versiones nuevas y muestra un botón **🔄 Actualizar** (manual/opt-in).

**Cuando el usuario pida "sube/saca/publica la versión X.Y.Z" (o similar):**

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
