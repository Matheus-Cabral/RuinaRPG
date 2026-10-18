# tools/migrations

`verificar-baseline.sh <ref da 1.5.0>` prova que a baseline de migrations da 2.0.0 equivale à cadeia de
84 migrations da 1.5.0 (spec `docs/superpowers/specs/2026-10-17-versao-2-0-0-b-migrations-design.md`,
B.3). Precisa de Docker, `dotnet-ef` e git; cria um Postgres e um worktree descartáveis e remove os dois
no fim. Uso: `tools/migrations/verificar-baseline.sh 729dac9`. Roda a cada ensaio do cutover (H); some
depois dele.
