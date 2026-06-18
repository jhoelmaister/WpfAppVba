---
description: Fusiona la rama de esta sesión con main y la elimina local y remoto
allowed-tools: Bash(git:*)
---

1. Detecta la rama actual con `git branch --show-current`.
   Si ya es `main` o `master`, avisa y no hagas nada más.
2. Cambia a master: `git checkout master`
3. Trae cambios remotos: `git pull`
4. Fusiona la rama del paso 1: `git merge <rama>`
5. Si el merge fue exitoso:
   - Borra local: `git branch -d <rama>`
   - Borra en remoto si existe: `git push origin --delete <rama>`
6. Mostrá el resultado con `git branch -a`
