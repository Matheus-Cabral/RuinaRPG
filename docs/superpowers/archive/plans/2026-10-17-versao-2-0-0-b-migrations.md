# Versão 2.0.0 — B: Migration squash Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the 84 EF Core migrations of 1.5.0 with one 2.0.0 baseline migration, rewrite the history of 1.5.0 databases automatically inside `--migrate`, and prove the schema and data are identical.

**Architecture:** A verification script builds old-chain and baseline databases in a throwaway Postgres and diffs `pg_dump` outputs. The persistence tests stop depending on named historical migrations. The baseline is generated from the unchanged model, with the `Pericias` `InsertData` copied in; `HistoricoDeMigracoes150.ConsolidarAsync` (Infrastructure), called first by `DatabaseInitializer`, swaps the exact 1.5.0 history for the baseline row and refuses any other old state.

**Tech Stack:** .NET 8, EF Core 8.0.10 + Npgsql, PostgreSQL 16, xUnit + FluentAssertions, bash, `dotnet-ef` (global tool, already installed), Docker.

**Spec:** `docs/superpowers/archive/specs/2026-10-17-versao-2-0-0-b-migrations-design.md`

## Global Constraints

- Baseline migration ID, exactly: `20261017120000_Baseline_2_0_0`.
- The 1.5.0 reference commit for every comparison: `729dac9` (`main` with 1.5.0 merged).
- The EF model (entities, `RuinaRpgDbContext.OnModelCreating`) is **not** changed in B. A schema difference is a bug in the baseline, never a reason to change the model.
- No `HasData` in the model; the `Pericias` seed stays an `InsertData` in the migration.
- `dotnet build` must end with 0 warnings, 0 errors (`--no-incremental` for the final check).
- TDD is mandatory (Técnico R0011): failing test first, then the code.
- Production (192.168.80.10) is never contacted. Docker resources created by scripts/tests use their own names and are removed at the end; the running `ruinarpg` compose stack on this VM is never touched.
- Code identifiers and user-facing/log messages follow the repo's style: pt-BR names for domain/infra concepts (`ConsolidarAsync`, `HistoricoDeMigracoes150`), pt-BR messages.
- Commit messages end with the session's attribution lines; push after each task (`git push`), branch `release/2.0.0`.

## Review Focus

1. Production's 1.5.0 history rows carry assorted `ProductVersion` values (8.0.x over time) → the rewrite must match on `MigrationId` only. Pinned in Task 3 (`DefinirHistoricoAsync` writes alternating versions).
2. A refusal must leave the history exactly as it was, `ProductVersion` included, and must not touch data → Task 3 refusal theory compares full `(MigrationId, ProductVersion)` rows and the user row.
3. A blank database has no `__EFMigrationsHistory` table at all → `ConsolidarAsync` must not throw and must not create it. Pinned in Task 3 test 1.
4. `dotnet run` picks up `Properties/launchSettings.json` (Development) unless told not to → the verification script uses `--no-launch-profile` and `ASPNETCORE_ENVIRONMENT=Production`, so `--migrate` is the only thing that migrates. Checked in Task 1 Step 3 by the A-vs-C comparison working at all.
5. `pg_dump` from recent Postgres 16 minors emits `\restrict <random token>` / `\unrestrict` lines → the normalizer drops them, or every comparison fails spuriously. Pinned in Task 1 (normalizer) and its positive run.

---

### Task 1: Verification script `tools/migrations/verificar-baseline.sh`

**Files:**
- Create: `tools/migrations/verificar-baseline.sh` (executable)
- Create: `tools/migrations/normalizar-dump.py`
- Create: `tools/migrations/README.md`

**Interfaces:**
- Produces: `tools/migrations/verificar-baseline.sh <ref-1.5.0>` → exit 0 when A0 = B0 (schema and full dump), A = C (full dump) and history(C) = history(B0); exit 1 with the diff printed otherwise. Used by Task 3 (Step 9) and later by H.

At this point the working tree still has the old chain, so A0 = B0 and A = C trivially: the positive run proves the plumbing, the sabotage run proves it detects differences.

- [ ] **Step 1: Write the normalizer**

`tools/migrations/normalizar-dump.py`:

```python
#!/usr/bin/env python3
"""Normalizes a plain pg_dump read from stdin, for diffing: drops the per-run \\restrict/\\unrestrict
token lines and sorts the rows of every COPY block, so physical row order never shows as a difference."""
import sys


def main() -> None:
    out = []
    rows = None
    for line in sys.stdin:
        if line.startswith("\\restrict") or line.startswith("\\unrestrict"):
            continue
        if rows is not None:
            if line == "\\.\n":
                out.extend(sorted(rows))
                out.append(line)
                rows = None
            else:
                rows.append(line)
            continue
        out.append(line)
        if line.startswith("COPY ") and line.rstrip().endswith("FROM stdin;"):
            rows = []
    sys.stdout.writelines(out)


if __name__ == "__main__":
    main()
```

- [ ] **Step 2: Write the script**

`tools/migrations/verificar-baseline.sh`:

```bash
#!/usr/bin/env bash
# Prova que a baseline da 2.0.0 equivale à cadeia de migrations da 1.5.0 (spec B, B.3).
#   uso: tools/migrations/verificar-baseline.sh <ref da 1.5.0>     (ex.: 729dac9)
# Num Postgres descartável, monta quatro bancos:
#   a0 = cadeia antiga só com migrations      b0 = árvore atual só com migrations
#   a  = --migrate da 1.5.0 (migrations + seeders)
#   c  = cópia de a + --migrate da árvore atual (o caminho de upgrade da produção)
# e exige: schema e dump completo a0 = b0; dump completo a = c; histórico de c = histórico de b0.
# __EFMigrationsHistory fica fora dos dumps. Sai com 1 e imprime o diff se algo diferir.
set -euo pipefail
shopt -s inherit_errexit

ref="${1:?uso: verificar-baseline.sh <ref da 1.5.0>}"
root="$(git rev-parse --show-toplevel)"
here="$(cd "$(dirname "$0")" && pwd)"
work="$(mktemp -d)"
name="ruina-verificar-baseline-$$"
image="postgres:16-alpine"

cleanup() {
  docker rm -f "$name" >/dev/null 2>&1 || true
  git -C "$root" worktree remove --force "$work/old" >/dev/null 2>&1 || true
  rm -rf "$work"
}
trap cleanup EXIT

step() { printf '== %s\n' "$*" >&2; }

step "worktree de $ref"
git -C "$root" worktree add --detach "$work/old" "$ref" >/dev/null 2>&1

step "postgres descartável ($name)"
docker run -d --name "$name" -e POSTGRES_PASSWORD=verificar -p 127.0.0.1::5432 "$image" >/dev/null
port="$(docker port "$name" 5432/tcp | head -1 | sed 's/.*://')"
for _ in $(seq 1 60); do
  docker exec "$name" pg_isready -U postgres -q && break
  sleep 1
done
docker exec "$name" pg_isready -U postgres -q

psql_() { docker exec -i "$name" psql -v ON_ERROR_STOP=1 -U postgres -qAt "$@"; }
cs() { printf 'Host=127.0.0.1;Port=%s;Database=%s;Username=postgres;Password=verificar' "$port" "$1"; }

for db in a0 b0 a; do psql_ -c "CREATE DATABASE $db"; done

export ASPNETCORE_ENVIRONMENT=Production
export Jwt__SigningKey="verificar-baseline-chave-descartavel-0123456789"
export Jwt__Issuer=verificar Jwt__Audience=verificar

run_logged() {
  local log="$work/$1.log"; shift
  if ! "$@" >"$log" 2>&1; then
    cat "$log" >&2
    printf 'falhou: %s\n' "$*" >&2
    exit 1
  fi
}

ef_update() {
  step "dotnet ef database update ($2)"
  run_logged "$2-ef" dotnet ef database update \
    --project "$1/src/RuinaRPG.Infrastructure" --startup-project "$1/src/RuinaRPG.Api" \
    --connection "$(cs "$2")"
}

migrate() {
  step "--migrate ($2)"
  ConnectionStrings__Postgres="$(cs "$2")" run_logged "$2-migrate" \
    dotnet run --no-launch-profile --project "$1/src/RuinaRPG.Api" -- --migrate
}

ef_update "$work/old" a0
ef_update "$root" b0
migrate "$work/old" a
psql_ -c "CREATE DATABASE c TEMPLATE a"
migrate "$root" c

dump() {
  docker exec "$name" pg_dump -U postgres --no-owner --no-privileges \
    --exclude-table='"__EFMigrationsHistory"' "$@" | python3 -I "$here/normalizar-dump.py"
}
historico() { psql_ -d "$1" -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1'; }

dump --schema-only a0 >"$work/a0.schema.sql"
dump --schema-only b0 >"$work/b0.schema.sql"
dump a0 >"$work/a0.sql"
dump b0 >"$work/b0.sql"
dump a >"$work/a.sql"
dump c >"$work/c.sql"
historico b0 >"$work/b0.hist"
historico c >"$work/c.hist"

status=0
compare() {
  if diff -u "$work/$1" "$work/$2" >"$work/diff" ; then
    printf 'OK    %s\n' "$3"
  else
    printf 'FALHA %s\n' "$3"
    cat "$work/diff"
    status=1
  fi
}
compare a0.schema.sql b0.schema.sql "schema: cadeia da 1.5.0 = baseline"
compare a0.sql b0.sql "dump completo (migrations): cadeia da 1.5.0 = baseline"
compare a.sql c.sql "dump completo (com seeders): 1.5.0 = 1.5.0 atualizada"
compare b0.hist c.hist "histórico: atualizado = novo"
exit "$status"
```

Then: `chmod +x tools/migrations/verificar-baseline.sh tools/migrations/normalizar-dump.py`.

- [ ] **Step 3: Positive run (identical trees)**

Run: `tools/migrations/verificar-baseline.sh 729dac9`
Expected: four `OK` lines, exit 0. (Takes a few minutes: two `dotnet ef` builds and two `dotnet run` builds.) If `dotnet ef` (tool 10.x) fails against the EF 8 project, report it as BLOCKED with the log; do not change package versions.

- [ ] **Step 4: Sabotage run (must detect a difference)**

Temporarily add, as the last line of `Up` in `src/RuinaRPG.Infrastructure/Persistence/Migrations/20261014100935_AddAutomaticoToSheetLogEntries.cs`:

```csharp
            migrationBuilder.Sql("CREATE TABLE \"Sabotagem\" (\"Id\" integer);");
```

Run: `tools/migrations/verificar-baseline.sh 729dac9; echo "exit=$?"`
Expected: `FALHA` lines for the schema and the two migrations-only/with-seeders comparisons where `Sabotagem` appears (a0 vs b0 at least), `exit=1`.

Then revert: `git checkout -- src/RuinaRPG.Infrastructure/Persistence/Migrations/20261014100935_AddAutomaticoToSheetLogEntries.cs` and check `git status --short` shows only the three new files. Also check `docker ps -a --filter name=ruina-verificar-baseline` is empty and `git worktree list` shows no leftover worktree.

- [ ] **Step 5: README**

`tools/migrations/README.md`:

```markdown
# tools/migrations

`verificar-baseline.sh <ref da 1.5.0>` prova que a baseline de migrations da 2.0.0 equivale à cadeia de
84 migrations da 1.5.0 (spec `docs/superpowers/archive/specs/2026-10-17-versao-2-0-0-b-migrations-design.md`,
B.3). Precisa de Docker, `dotnet-ef` e git; cria um Postgres e um worktree descartáveis e remove os dois
no fim. Uso: `tools/migrations/verificar-baseline.sh 729dac9`. Roda a cada ensaio do cutover (H); some
depois dele.
```

- [ ] **Step 6: Commit**

```bash
git add tools/migrations
git commit -m "feat(tools): verificar-baseline.sh compara a cadeia da 1.5.0 com a árvore atual"
git push
```

---

### Task 2: Persistence tests stop depending on historical migrations

**Files:**
- Delete: `tests/RuinaRPG.Tests.Integration/Persistence/AfinidadeSegundaEssenciaMigrationTests.cs`, `DurabilidadeBackfillMigrationTests.cs`, `EncounterPhasesMigrationTests.cs`, `PericiasComoTabelaMigrationTests.cs`, `RequisitosLegadosMigrationTests.cs`, `SecretNoteReadAtMigrationTests.cs`, `SchemaInsert.cs`
- Modify: every other `tests/RuinaRPG.Tests.Integration/Persistence/*.cs` that calls `GetAppliedMigrationsAsync` (list in Step 2)
- Modify: `tests/RuinaRPG.Tests.Integration/Persistence/MigrationTests.cs`, `DurabilidadePorRankMigrationTests.cs`

**Interfaces:**
- Produces: no test in the solution references a migration by name or migrates to an intermediate target (`grep -rn "GetAppliedMigrations\|IMigrator\|GetMigrations()" tests` is empty). Task 3 relies on this: after the squash the old names no longer exist.

Triage (the spec's rule, applied per file):

| Verdict | Files | Why |
|---|---|---|
| Remove | `AfinidadeSegundaEssenciaMigrationTests`, `DurabilidadeBackfillMigrationTests`, `EncounterPhasesMigrationTests`, `PericiasComoTabelaMigrationTests`, `RequisitosLegadosMigrationTests`, `SecretNoteReadAtMigrationTests` | Migrate to an intermediate migration with `IMigrator`, insert legacy rows and assert a backfill — history that no longer exists. |
| Remove | `SchemaInsert.cs` | Only the six files above use it. |
| Remove one fact | `DurabilidadePorRankMigrationTests.Migrate_creates_the_AddDurabilidadePorRank_migration` | Asserts only that a named migration was applied. |
| Rewrite | `MigrationTests` | Its only assertion is the `InitialCreate` name; it becomes the "no pending migration, model matches migrations" test. |
| Keep, drop the applied-migration assertion | All other files that call `GetAppliedMigrationsAsync` | Their remaining assertions check the current schema (tables, columns, nullability, defaults, FKs, cascades, unique indexes) or seeders. They keep `BlankDatabaseFixture` + full `MigrateAsync()`, which is cheap with one migration. |
| Unchanged | `DatabaseInitializerTests`, `EquipmentKitSchemaTests`, `SubcategoriaOptionSchemaTests`, `TabelaDeAfinidadesSeederTests`, `CondicoesMigrationTests`, `EncounterLogOrdemMigrationTests`, `EncounterParticipantCondicoesMigrationTests`, `PericiasMigrationTests` | No reference to history. `PericiasMigrationTests` becomes the guard of the baseline's `Pericias` seed. |

- [ ] **Step 1: Delete the history-only files**

```bash
cd tests/RuinaRPG.Tests.Integration/Persistence
git rm -q AfinidadeSegundaEssenciaMigrationTests.cs DurabilidadeBackfillMigrationTests.cs EncounterPhasesMigrationTests.cs \
  PericiasComoTabelaMigrationTests.cs RequisitosLegadosMigrationTests.cs SecretNoteReadAtMigrationTests.cs SchemaInsert.cs
cd -
```

- [ ] **Step 2: Remove the applied-migration assertions**

In each of these files, delete the statement(s) that read `GetAppliedMigrationsAsync` and the assertion on their result — either the two-line form

```csharp
        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddItems"));
```

or the one-line form

```csharp
        (await db.Database.GetAppliedMigrationsAsync()).Should().Contain(m => m.EndsWith("AddBonusDeCargaDaCampanha"));
```

Files: `CampaignAttachmentMigrationTests`, `CampaignBonusDeCargaMigrationTests`, `CampaignMemberMigrationTests`, `CampaignMigrationTests`, `CharacterAffectionAndTraitMigrationTests`, `CharacterAffinityMigrationTests`, `CharacterArsenalMigrationTests`, `CharacterAttributeAndSkillMigrationTests`, `CharacterInventoryAndArtifactMigrationTests`, `CharacterRuneAndMasteryMigrationTests`, `CharacterSheetMigrationTests`, `CharacterSpellAbilityMigrationTests`, `CreatureChildTableMigrationTests`, `CreatureExclusiveTraitMigrationTests`, `CreatureSheetMigrationTests`, `DiaryEntryCharacterSheetForeignKeyMigrationTests`, `DiaryMigrationTests`, `DurabilidadePorRankMigrationTests`, `EfeitoMigrationAndSeedTests`, `EncounterMigrationTests`, `HabilidadesPassivasMigrationTests`, `HistoriaImagensMigrationTests`, `ImageMigrationTests`, `InviteCodeMigrationTests`, `ItemMigrationTests`, `NpcChildTableMigrationTests`, `NpcSheetMigrationTests`, `RacialAbilityMigrationAndArcaEntryTests`, `RacialTraitMigrationTests`, `RulebookDocumentOverrideMigrationTests`, `RulesAuditorMigrationTests`, `RuneBankMigrationTests`, `SheetLogEntryMigrationTests`, `SpellAbilityBankMigrationTests`, `SpellAbilityCriadaNaFichaMigrationTests`, `TabelaDeNiveisConfigMigrationTests`, `TraitAuditFieldsMigrationTests`, `TraitMigrationAndSeedTests`, `TraitMigrationTests`.

Every other line stays. If a `using` becomes unused (build warning), remove it. A fact whose body is left with no assertion at all must not exist: if that happens, report it instead of deleting the fact (none is expected; the survey found other assertions in each).

In `DurabilidadePorRankMigrationTests.cs` also delete the whole fact `Migrate_creates_the_AddDurabilidadePorRank_migration` (its `[Fact]` attribute and method).

- [ ] **Step 3: Rewrite `MigrationTests`**

Replace the body of `tests/RuinaRPG.Tests.Integration/Persistence/MigrationTests.cs` with:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class MigrationTests : IClassFixture<BlankDatabaseFixture>
{
    private readonly BlankDatabaseFixture _fixture;

    public MigrationTests(BlankDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_leaves_nothing_pending_and_the_model_matches_the_migrations()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        db.Database.HasPendingModelChanges().Should().BeFalse();
        (await db.Users.CountAsync()).Should().Be(0);
        (await db.RefreshTokens.CountAsync()).Should().Be(0);
    }
}
```

(`db.Users` and `db.RefreshTokens` keep the old test's intent: the tables exist and are queryable. If `RefreshTokens` is not the `DbSet` name, use the actual name from `RuinaRpgDbContext`.)

- [ ] **Step 4: Verify nothing references history any more**

Run: `grep -rn "GetAppliedMigrations\|IMigrator\|GetMigrations()\|SchemaInsert" tests src --include=*.cs | grep -v "/Migrations/"`
Expected: no output.

- [ ] **Step 5: Build and run the persistence tests**

Run: `dotnet build 2>&1 | tail -3` → `0 Warning(s)`, `0 Error(s)`.
Run: `dotnet test tests/RuinaRPG.Tests.Integration --no-build --filter "FullyQualifiedName~RuinaRPG.Tests.Integration.Persistence"`
Expected: all pass (count = previous count of the namespace minus 7: six removed facts from the deleted files plus the one removed `DurabilidadePorRank` fact; record both numbers in the report).

- [ ] **Step 6: Commit**

```bash
git add -A tests/RuinaRPG.Tests.Integration/Persistence
git commit -m "test: testes de persistência deixam de depender de migrations históricas"
git push
```

---

### Task 3: Baseline migration + history rewrite inside `--migrate`

**Files:**
- Create: `src/RuinaRPG.Infrastructure/Persistence/HistoricoDeMigracoes150.cs`
- Create: `tests/RuinaRPG.Tests.Integration/Persistence/ConsolidacaoDoHistoricoTests.cs`
- Modify: `src/RuinaRPG.Api/DatabaseInitializer.cs` (call before `MigrateAsync`)
- Modify: `tests/RuinaRPG.Tests.Integration/Persistence/DatabaseInitializerTests.cs` (one new fact)
- Modify: `tests/RuinaRPG.Tests.Integration/TemplateDatabase.cs` (comment says "169 migrations")
- Delete: everything in `src/RuinaRPG.Infrastructure/Persistence/Migrations/`
- Create: `src/RuinaRPG.Infrastructure/Persistence/Migrations/20261017120000_Baseline_2_0_0.cs`, `.Designer.cs`, `RuinaRpgDbContextModelSnapshot.cs` (generated)

**Interfaces:**
- Consumes: Task 1's `tools/migrations/verificar-baseline.sh`; Task 2's history-free tests.
- Produces: `public static class HistoricoDeMigracoes150` in namespace `RuinaRPG.Infrastructure.Persistence` with `public const string Baseline = "20261017120000_Baseline_2_0_0";`, `public static readonly IReadOnlyList<string> Ids` (the 84 IDs) and `public static Task ConsolidarAsync(RuinaRpgDbContext db, ILogger logger)`.

The history rewrite and the squash land together: the rewrite before the squash would point 1.5.0 databases at a migration that does not exist yet, and the squash without the rewrite breaks every 1.5.0 database.

- [ ] **Step 1: Write the failing tests**

`tests/RuinaRPG.Tests.Integration/Persistence/ConsolidacaoDoHistoricoTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

// Cada teste mexe no histórico de migrations, então cada um ganha seu próprio banco vazio.
public class ConsolidacaoDoHistoricoTests
{
    private static async Task ComBancoNovoAsync(Func<RuinaRpgDbContext, Task> corpo)
    {
        var banco = new BlankDatabaseFixture();
        await banco.InitializeAsync();
        try
        {
            var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(banco.ConnectionString).Options;
            await using var db = new RuinaRpgDbContext(options);
            await corpo(db);
        }
        finally
        {
            await banco.DisposeAsync();
        }
    }

    private static Task<List<string>> HistoricoAsync(RuinaRpgDbContext db) =>
        db.Database.SqlQueryRaw<string>("SELECT \"MigrationId\" AS \"Value\" FROM \"__EFMigrationsHistory\" ORDER BY 1").ToListAsync();

    private static Task<List<string>> LinhasDoHistoricoAsync(RuinaRpgDbContext db) =>
        db.Database.SqlQueryRaw<string>("SELECT \"MigrationId\" || '|' || \"ProductVersion\" AS \"Value\" FROM \"__EFMigrationsHistory\" ORDER BY 1").ToListAsync();

    // Produção acumulou linhas gravadas por várias versões do EF 8; a consolidação olha só o MigrationId.
    private static async Task DefinirHistoricoAsync(RuinaRpgDbContext db, IEnumerable<string> ids)
    {
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"__EFMigrationsHistory\"");
        var i = 0;
        foreach (var id in ids)
        {
            var versao = i++ % 2 == 0 ? "8.0.8" : "8.0.10";
            await db.Database.ExecuteSqlAsync($"INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({id}, {versao})");
        }
    }

    private static async Task<ApplicationUser> CriarGmAsync(RuinaRpgDbContext db)
    {
        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@consolidacao.com", Email = "gm@consolidacao.com", Nickname = "ConsolidacaoGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();
        return gm;
    }

    private static async Task<ApplicationUser?> GmAsync(RuinaRpgDbContext db, Guid id) =>
        await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id);

    [Fact]
    public async Task Banco_novo_nao_e_tocado_e_o_migrate_cria_a_baseline_com_as_pericias()
    {
        await ComBancoNovoAsync(async db =>
        {
            await HistoricoDeMigracoes150.ConsolidarAsync(db, NullLogger.Instance);

            (await db.Database.SqlQueryRaw<bool>("SELECT to_regclass('public.\"__EFMigrationsHistory\"') IS NULL AS \"Value\"").SingleAsync())
                .Should().BeTrue("a consolidação não cria a tabela de histórico");

            await db.Database.MigrateAsync();

            (await HistoricoAsync(db)).Should().Equal(HistoricoDeMigracoes150.Baseline);
            (await db.Pericias.CountAsync()).Should().Be(39);
        });
    }

    [Fact]
    public async Task Banco_na_1_5_0_troca_o_historico_pela_baseline_sem_tocar_nos_dados()
    {
        await ComBancoNovoAsync(async db =>
        {
            await db.Database.MigrateAsync();
            var gm = await CriarGmAsync(db);
            await DefinirHistoricoAsync(db, HistoricoDeMigracoes150.Ids);
            var pericias = await db.Pericias.CountAsync();

            await HistoricoDeMigracoes150.ConsolidarAsync(db, NullLogger.Instance);

            (await HistoricoAsync(db)).Should().Equal(HistoricoDeMigracoes150.Baseline);
            (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
            (await GmAsync(db, gm.Id))!.Nickname.Should().Be("ConsolidacaoGm");
            (await db.Pericias.CountAsync()).Should().Be(pericias);
        });
    }

    [Fact]
    public async Task Consolidar_duas_vezes_nao_muda_nada_na_segunda()
    {
        await ComBancoNovoAsync(async db =>
        {
            await db.Database.MigrateAsync();
            await DefinirHistoricoAsync(db, HistoricoDeMigracoes150.Ids);
            await HistoricoDeMigracoes150.ConsolidarAsync(db, NullLogger.Instance);
            var depoisDaPrimeira = await LinhasDoHistoricoAsync(db);

            await HistoricoDeMigracoes150.ConsolidarAsync(db, NullLogger.Instance);

            (await LinhasDoHistoricoAsync(db)).Should().Equal(depoisDaPrimeira);
        });
    }

    [Fact]
    public async Task Baseline_seguida_de_migrations_novas_fica_como_esta()
    {
        await ComBancoNovoAsync(async db =>
        {
            await db.Database.MigrateAsync();
            await DefinirHistoricoAsync(db, [HistoricoDeMigracoes150.Baseline, "20991231000000_Futura"]);
            var antes = await LinhasDoHistoricoAsync(db);

            await HistoricoDeMigracoes150.ConsolidarAsync(db, NullLogger.Instance);

            (await LinhasDoHistoricoAsync(db)).Should().Equal(antes);
        });
    }

    public static TheoryData<string> EstadosRecusados => new() { "parcial", "desconhecida", "misturado" };

    private static IEnumerable<string> Historico(string estado) => estado switch
    {
        "parcial" => HistoricoDeMigracoes150.Ids.Take(HistoricoDeMigracoes150.Ids.Count - 1),
        "desconhecida" => HistoricoDeMigracoes150.Ids.Append("20261015000000_Desconhecida"),
        "misturado" => HistoricoDeMigracoes150.Ids.Take(10).Append(HistoricoDeMigracoes150.Baseline),
        _ => throw new ArgumentOutOfRangeException(nameof(estado)),
    };

    [Theory]
    [MemberData(nameof(EstadosRecusados))]
    public async Task Qualquer_outro_historico_e_recusado_sem_mudar_nada(string estado)
    {
        await ComBancoNovoAsync(async db =>
        {
            await db.Database.MigrateAsync();
            var gm = await CriarGmAsync(db);
            await DefinirHistoricoAsync(db, Historico(estado));
            var antes = await LinhasDoHistoricoAsync(db);

            await FluentActions.Awaiting(() => HistoricoDeMigracoes150.ConsolidarAsync(db, NullLogger.Instance))
                .Should().ThrowAsync<InvalidOperationException>().WithMessage("*1.5.0*Nada foi alterado*");

            (await LinhasDoHistoricoAsync(db)).Should().Equal(antes);
            (await GmAsync(db, gm.Id)).Should().NotBeNull();
        });
    }
}
```

Add to `DatabaseInitializerTests` (same file, after the existing facts; it already has `BuildServices()` and `CountSeededTablesAsync`):

```csharp
    [Fact]
    public async Task Initializing_a_1_5_0_database_consolidates_its_history_and_keeps_the_data()
    {
        await using var services = BuildServices();
        await DatabaseInitializer.InitializeAsync(services, NullLogger.Instance);
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            await db.Database.ExecuteSqlRawAsync("DELETE FROM \"__EFMigrationsHistory\"");
            foreach (var id in HistoricoDeMigracoes150.Ids)
                await db.Database.ExecuteSqlAsync($"INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({id}, '8.0.10')");
        }
        var before = await CountSeededTablesAsync(services);

        await DatabaseInitializer.InitializeAsync(services, NullLogger.Instance);

        await using var check = services.CreateAsyncScope();
        var migrated = check.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        (await migrated.Database.GetAppliedMigrationsAsync()).Should().Equal(HistoricoDeMigracoes150.Baseline);
        (await CountSeededTablesAsync(services)).Should().Equal(before);
    }
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet build tests/RuinaRPG.Tests.Integration 2>&1 | grep -m3 "error"`
Expected: `CS0103`/`CS0246` — `HistoricoDeMigracoes150` does not exist.

- [ ] **Step 3: Write `HistoricoDeMigracoes150`**

`src/RuinaRPG.Infrastructure/Persistence/HistoricoDeMigracoes150.cs` (the `Ids` list is the 84 migrations of 1.5.0, in order — copy it verbatim):

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace RuinaRPG.Infrastructure.Persistence;

/// As 84 migrations da 1.5.0 foram consolidadas na baseline da 2.0.0, que cria exatamente o mesmo schema
/// (provado por tools/migrations/verificar-baseline.sh). Um banco que está exatamente na 1.5.0 tem o
/// histórico trocado pela baseline, sem tocar em schema nem dados; qualquer outro estado antigo é recusado.
/// Roda dentro de --migrate (DatabaseInitializer), antes do MigrateAsync.
public static class HistoricoDeMigracoes150
{
    public const string Baseline = "20261017120000_Baseline_2_0_0";

    public static readonly IReadOnlyList<string> Ids =
    [
        "20260815020559_InitialCreate",
        "20260815103216_AddNormalizedNicknameUniqueIndex",
        "20260816033827_AddInviteCodes",
        "20260817123015_AddImages",
        "20260817125005_AddItems",
        "20260817174147_AddSpellAbilityBank",
        "20260821214356_AddCampaigns",
        "20260821215316_AddCampaignMembers",
        "20260821220851_AddDiary",
        "20260822020827_AddCharacterSheets",
        "20260822084307_AddCharacterAttributesAndSkills",
        "20260822091219_AddCharacterAffinities",
        "20260822092330_AddCharacterArsenal",
        "20260822134358_AddCharacterSpellAbilities",
        "20260822140810_AddCharacterRunesAndMasteries",
        "20260822214510_AddCharacterInventoryAndArtifacts",
        "20260822220342_AddTraits",
        "20260822220856_AddCharacterAffectionsAndTraits",
        "20260822224256_AddDiaryEntryCharacterSheetForeignKey",
        "20260823032923_AddNpcSheets",
        "20260823034231_AddNpcChildTables",
        "20260823172112_AddCreatureSheets",
        "20260823173404_AddCreatureChildTables",
        "20260823183411_DropCreatureSheetPropriedade",
        "20260827130556_AddCampaignAttachments",
        "20260827181159_AddEncounters",
        "20260829135336_AddSkillAtributoEscolhido",
        "20260829234234_EncounterCurrentParticipantId",
        "20260902223521_AddCampaignImage",
        "20260903013750_AddCharacterSheetPontosDeIgnicaoBonusManual",
        "20260903022715_AddCharacterSheetPontosDePericiaBonusCritico",
        "20260903161021_AddAffinityElementoAndSubElementoValor",
        "20260903165921_MakeAffinityFieldsNullable",
        "20260908145837_AddItemGeralCapacidadeExtra",
        "20260908205847_AddTraitSpecificationFields",
        "20260909154649_AddRacialAbilityOverrideAndArcaEntry",
        "20260911160057_AddRacialTraits",
        "20260911172033_AddIsRulesAuditor",
        "20260911174405_AddTraitAuditFields",
        "20260911185514_AddRulebookDocumentOverrides",
        "20260914162951_AddCreatureExclusiveTraits",
        "20260914172924_AddCreatureTraitExactlyOneCatalogCheck",
        "20260914204155_AddEfeitos",
        "20260918023137_AddLastSeenAppVersion",
        "20260918140611_AddMustChangePassword",
        "20260921142649_AddNomeDaVarianteToRacialAbilityOverride",
        "20260921160223_AddRuneBank",
        "20260921173257_AddRuneImage",
        "20260922135852_AddCharacterSheetAndNpcSheetEstrelaSina",
        "20260922160637_AddHistoricosAndSheetHistoricoId",
        "20260923020827_AddEquipmentKitsAndSheetEquipmentKitId",
        "20260923144713_AddSubcategoriaOptionsAndArmorSlot",
        "20260926011207_AddSheetHistoria",
        "20260926031550_AddAfinidadeSegundaEssencia",
        "20260926042942_DropAfinidadeCaminhoNome",
        "20260927173324_AddAfinidadeAdicional",
        "20260927194425_AddSpellAbilityBankDeCriatura",
        "20260928194344_AddHabilidadesPassivas",
        "20260929005951_AddDurabilidadePorRank",
        "20260929012531_RemoveDurabilidadeMaximaDosItens",
        "20260929082529_RulebookOverrideAutorOpcional",
        "20260929161238_AddArcaEvolucoes",
        "20261001100828_AddPericias",
        "20261001101601_PericiasComoTabela",
        "20261001113341_AddTabelaDeNiveis",
        "20261001183018_AddTipoDeRuna",
        "20261002005956_AddDadoDaTabelaDeArcas",
        "20261002025413_AddTabelaDeNiveisConfig",
        "20261002032113_AddBonusDeCargaDaCampanha",
        "20261002034641_AddImagensDaHistoria",
        "20261005172005_AddDisciplinaDeRuna",
        "20261005174723_AddRequisitosDeEquipamento",
        "20261005175651_ConverteRequisitosLegadosDeEquipamento",
        "20261005193357_AddEquipmentKitFixedItems",
        "20261009134906_AddTabelaDeAfinidades",
        "20261009180612_AddReadAtToDiaryEntryRecipients",
        "20261013152921_AddCriadaNaFichaToSheetSpellAbilities",
        "20261013171712_AddSheetLogEntries",
        "20261013194126_AddEncounterPhasesAndLog",
        "20261013201021_AddOrdemToEncounterLogEntries",
        "20261014004619_AddEncounterRowVersion",
        "20261014012056_AddCondicoes",
        "20261014020941_AddEncounterParticipantCondicoes",
        "20261014100935_AddAutomaticoToSheetLogEntries",
    ];

    public static async Task ConsolidarAsync(RuinaRpgDbContext db, ILogger logger)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            var connection = (NpgsqlConnection)db.Database.GetDbConnection();
            await using var transaction = await connection.BeginTransactionAsync();

            await using (var existe = new NpgsqlCommand("SELECT to_regclass('public.\"__EFMigrationsHistory\"') IS NOT NULL", connection, transaction))
            {
                if (!(bool)(await existe.ExecuteScalarAsync())!)
                    return;
            }

            await using (var trava = new NpgsqlCommand("LOCK TABLE \"__EFMigrationsHistory\" IN ACCESS EXCLUSIVE MODE", connection, transaction))
                await trava.ExecuteNonQueryAsync();

            var aplicadas = new List<string>();
            await using (var leitura = new NpgsqlCommand("SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\"", connection, transaction))
            await using (var reader = await leitura.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    aplicadas.Add(reader.GetString(0));
            }

            if (aplicadas.Count == 0)
                return;

            if (aplicadas.Contains(Baseline))
            {
                if (aplicadas.Any(Ids.Contains))
                    throw Recusa(aplicadas);
                return;
            }

            if (!aplicadas.ToHashSet().SetEquals(Ids))
                throw Recusa(aplicadas);

            await using (var apaga = new NpgsqlCommand("DELETE FROM \"__EFMigrationsHistory\"", connection, transaction))
                await apaga.ExecuteNonQueryAsync();
            await using (var insere = new NpgsqlCommand("INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES (@id, @versao)", connection, transaction))
            {
                insere.Parameters.AddWithValue("id", Baseline);
                insere.Parameters.AddWithValue("versao", ProductInfo.GetVersion());
                await insere.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();

            logger.LogInformation("Histórico de migrations consolidado: as {Quantidade} migrations da 1.5.0 viraram a baseline {Baseline}", Ids.Count, Baseline);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static InvalidOperationException Recusa(List<string> aplicadas) => new(
        $"O histórico de migrations não está na 1.5.0 nem na 2.0.0: esperado exatamente as {Ids.Count} migrations da 1.5.0 " +
        $"(última {Ids[^1]}) ou a baseline {Baseline}; encontradas {aplicadas.Count}, a mais recente {aplicadas.Max(StringComparer.Ordinal)}. " +
        "Nada foi alterado.");
}
```

If `Npgsql` is not directly referenced by Infrastructure, it is transitively (via `Npgsql.EntityFrameworkCore.PostgreSQL`) — no package change needed. If `ProductInfo.GetVersion()` raises an analyzer warning, replace it with `typeof(DbContext).Assembly.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0]`.

- [ ] **Step 4: Call it from `DatabaseInitializer`**

In `src/RuinaRPG.Api/DatabaseInitializer.cs`, replace

```csharp
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        await db.Database.MigrateAsync();
```

with

```csharp
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        await HistoricoDeMigracoes150.ConsolidarAsync(db, logger);
        await db.Database.MigrateAsync();
```

and update the class comment's first sentence to: `/// Consolidates a 1.5.0 migration history into the 2.0.0 baseline, applies pending migrations and runs every seeder/backfill, in order.`

- [ ] **Step 5: Run the new tests — the squash is still missing**

Run: `dotnet build 2>&1 | tail -3` then `dotnet test tests/RuinaRPG.Tests.Integration --no-build --filter "FullyQualifiedName~ConsolidacaoDoHistoricoTests|FullyQualifiedName~DatabaseInitializerTests"`
Expected: FAIL — at least `Banco_novo_nao_e_tocado_e_o_migrate_cria_a_baseline_com_as_pericias` (history is the 84 old IDs, not the baseline) and the 1.5.0 cases (after the rewrite, `GetPendingMigrations` lists the 84 old ones).

- [ ] **Step 6: Squash the migrations**

```bash
git show HEAD:src/RuinaRPG.Infrastructure/Persistence/Migrations/20261001100828_AddPericias.cs > /tmp/AddPericias.cs.orig
git rm -q -r src/RuinaRPG.Infrastructure/Persistence/Migrations
dotnet ef migrations add Baseline_2_0_0 --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations
cd src/RuinaRPG.Infrastructure/Persistence/Migrations
gen=$(ls *_Baseline_2_0_0.cs | grep -v Designer | sed 's/_Baseline_2_0_0\.cs$//')
mv "${gen}_Baseline_2_0_0.cs" 20261017120000_Baseline_2_0_0.cs
mv "${gen}_Baseline_2_0_0.Designer.cs" 20261017120000_Baseline_2_0_0.Designer.cs
sed -i "s/\[Migration(\"${gen}_Baseline_2_0_0\")\]/[Migration(\"20261017120000_Baseline_2_0_0\")]/" 20261017120000_Baseline_2_0_0.Designer.cs
grep -n 'Migration("' 20261017120000_Baseline_2_0_0.Designer.cs
cd -
```

Expected: the grep shows `[Migration("20261017120000_Baseline_2_0_0")]`; the folder has exactly three files.

Then copy the seed: in `20261017120000_Baseline_2_0_0.cs`, find the `migrationBuilder.CreateTable(name: "Pericias", ...);` statement in `Up` and insert, right after it, the whole `migrationBuilder.InsertData(table: "Pericias", ...);` statement copied verbatim from `/tmp/AddPericias.cs.orig` (it starts at the line `migrationBuilder.InsertData(` and ends at the matching `});`). Keep the original indentation (12 spaces). Then `rm /tmp/AddPericias.cs.orig`.

- [ ] **Step 7: Check the model and the build**

Run: `dotnet ef migrations has-pending-model-changes --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api`
Expected: "No changes have been made to the model since the last migration."

Run: `dotnet build --no-incremental 2>&1 | tail -3` → 0 warnings, 0 errors.

Update the comment in `tests/RuinaRPG.Tests.Integration/TemplateDatabase.cs` from "so no class pays for 169 migrations and seven seeders again" to "so no class pays for the migrations and seeders again".

- [ ] **Step 8: Run the tests**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --no-build --filter "FullyQualifiedName~RuinaRPG.Tests.Integration.Persistence"`
Expected: all pass, including the 7 `ConsolidacaoDoHistoricoTests` cases and `PericiasMigrationTests`.

Run the full suite: `dotnet test --no-build 2>&1 | grep -E "Passed!|Failed!"`
Expected: three `Passed!` lines (Unit, Client, Integration). Record the counts.

- [ ] **Step 9: Equivalence proof**

Run: `tools/migrations/verificar-baseline.sh 729dac9`
Expected: four `OK` lines, exit 0. Any `FALHA` is a defect of the baseline (Global Constraints: the model is not changed). Typical causes and fixes: a seed row missing (InsertData not copied or misplaced); a constraint/index name differing because the old chain renamed it (keep the old chain's name by editing the baseline's generated `name:` and re-check `has-pending-model-changes`; if the model forces the new name, report BLOCKED with the diff). Paste the four result lines into the report.

- [ ] **Step 10: Commit**

```bash
git add -A src/RuinaRPG.Infrastructure/Persistence src/RuinaRPG.Api/DatabaseInitializer.cs tests/RuinaRPG.Tests.Integration
git commit -m "feat(db): baseline 2.0.0 substitui as 84 migrations; --migrate consolida o histórico da 1.5.0"
git push
```

---

### Task 4: Docs, measurements and H notes

**Files:**
- Modify: `Docs/Requisitos/Requisitos - Técnico.md` (R0009)
- Modify: `CLAUDE.md` (EF Core migrations bullet)
- Modify: `docs/superpowers/specs/2026-10-16-versao-2-0-0-baseline.md` (new "After B" section)
- Modify: `docs/superpowers/specs/2026-10-17-versao-2-0-0-h-notas-do-cutover.md` (B notes)

**Interfaces:**
- Consumes: Task 3's baseline and `verificar-baseline.sh` results.

- [ ] **Step 1: R0009**

In `Docs/Requisitos/Requisitos - Técnico.md`, after the paragraph that starts with `**Versão 1.5.0**:` under R0009, add:

```markdown
**Versão 2.0.0**: as 84 migrations da 1.5.0 foram consolidadas numa única migration de baseline (`20261017120000_Baseline_2_0_0`), que cria o mesmo schema e as mesmas Perícias base. O `--migrate` reconhece um banco que está exatamente na 1.5.0 e troca o histórico de migrations dele pela baseline, sem tocar em schema nem dados; qualquer outro estado antigo (parte da cadeia, migrations desconhecidas) é recusado sem alterar nada, e o deploy aborta. Migrations novas entram por cima da baseline, como antes.
```

- [ ] **Step 2: CLAUDE.md**

Replace the bullet

```markdown
- EF Core migrations: `dotnet ef migrations add <Name> --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations`.
```

with

```markdown
- EF Core migrations: `dotnet ef migrations add <Name> --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations`. Since 2.0.0 the chain starts at one baseline (`20261017120000_Baseline_2_0_0`); `--migrate` converts a 1.5.0 history to it (`HistoricoDeMigracoes150`, Técnico R0009).
```

- [ ] **Step 3: Measure**

Run `tools/bench/measure-build-and-tests.sh after-b` once (the label is the first argument; results land in `tools/bench/results/after-b.txt`). Also record: `ls src/RuinaRPG.Infrastructure/Persistence/Migrations | wc -l` and `du -sh src/RuinaRPG.Infrastructure/Persistence/Migrations`, and `tools/bench/measure-code-size.sh after-b`.

- [ ] **Step 4: Baseline doc**

Append to `docs/superpowers/specs/2026-10-16-versao-2-0-0-baseline.md`:

```markdown
## After B (migration squash)

Measured on `<sha>` (release/2.0.0), same VM, one run.

| Area | Metric | After A | After B |
|---|---|---|---|
| Build | `dotnet build --no-incremental` (s) | 74.9 / 128.5 / 72.9 | <value> |
| Tests | Integration, one process: time (s) | 453.1 / 432.9 / 432.5 | <value> |
| Tests | Integration, one process: result | 1806 passed | <passed>, <failed> failed (of <total>) |
| Code size | migrations | 169 files, 9.6 MB | <files> files, <size> |
| Code size | test projects `.cs` lines | Unit 6695; Client 17334; Integration 40305 | <values> |

`verificar-baseline.sh 729dac9`: <the four OK lines>.
```

with every `<…>` replaced by the measured value (the integration count drops by the removed history tests and rises by the 8 new ones: 7 consolidation cases + 1 initializer fact).

- [ ] **Step 5: H notes**

Append to `docs/superpowers/specs/2026-10-17-versao-2-0-0-h-notas-do-cutover.md`:

```markdown
## From sub-project B (migration squash)

- Requirement 1 above is done: `DatabaseInitializer` calls `HistoricoDeMigracoes150.ConsolidarAsync` before `MigrateAsync`, so the `--migrate` that `deploy.sh` runs converts the 1.5.0 history to the baseline `20261017120000_Baseline_2_0_0` in one transaction, and refuses any other old state without changing anything.
- Every rehearsal runs `tools/migrations/verificar-baseline.sh <1.5.0 sha>` and requires four `OK` lines.
- Rollback to 1.5.0: after the cutover the history holds only the baseline row, which 1.5.0 does not know. A 1.5.0 API still runs (Production never migrates at startup and the schema is identical), but 1.5.0's `--migrate` (`make migrate`, `make deploy` of 1.5.0) would try to recreate the whole schema and fail. So the rollback in step 11 must restore the pre-cutover backup (as written) and must never run 1.5.0's migrate against the converted database.
```

- [ ] **Step 6: Commit**

```bash
git add "Docs/Requisitos/Requisitos - Técnico.md" CLAUDE.md docs/superpowers/specs/2026-10-16-versao-2-0-0-baseline.md docs/superpowers/specs/2026-10-17-versao-2-0-0-h-notas-do-cutover.md
git commit -m "docs: baseline de migrations da 2.0.0 (R0009, CLAUDE.md, medições e notas do H)"
git push
```
