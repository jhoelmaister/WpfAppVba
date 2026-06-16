# Git hooks

Para activar estos hooks en tu checkout local, corré una vez:

```
git config core.hooksPath scripts/hooks
```

## pre-commit

Bloquea cualquier `git commit` si la rama actual empieza con `claude/`.
