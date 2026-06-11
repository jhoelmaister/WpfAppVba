# CLAUDE.md — WpfAppVba-sistemaControl

## REGLA CRÍTICA - Control de versiones (Git)
- NUNCA ejecutes git checkout -b ni ningún comando que cree una rama nueva.
- Trabajá EXCLUSIVAMENTE sobre la rama que ya está activa al iniciar la sesión.
- Antes de hacer cualquier commit, verificá la rama actual con git branch --show-current.
- Si por algún motivo ya estás en una rama nueva tipo claude/... que no fue pedida, avisá al usuario antes de continuar y preguntá si hacer checkout de vuelta a la rama original.
- No crear ramas intermedias de sesión bajo ningún concepto.

## Reglas obligatorias de cada sesión

1. Contexto primero
   - Leer CONTEXT.md y el código existente relevante antes de proponer cualquier cambio.
   - No reinventar patrones ya establecidos (estilos, nombres, estructura del design system).

2. Plan antes de ejecutar
   - Mostrar un resumen del plan (archivos, cambios, motivo).
   - Esperar confirmación explícita del usuario antes de aplicar nada.

3. Ante la duda
   - Preguntar antes de asumir, especialmente en cambios estructurales o que afecten varios archivos.

Estas reglas aplican durante toda la sesión, no solo al inicio.
