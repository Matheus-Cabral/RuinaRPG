# Versão 1.5.0 — Plano C: Encontros — fases, vez, finalizados e log

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give an encounter three phases (Turno 0 / em andamento / finalizado), explicit control of whose turn it is (next, previous, choose), a separate list of finished encounters, and a log recorded from Turno 1 that also captures resource changes made on linked sheets.

**Architecture:** The phase is one enum column on `Encounter`; all turn arithmetic stays in the pure `TurnAdvanceCalculator`. Encounter events are written by the two encounter controllers through one small scoped service, in the same save as the change. Resource changes on linked sheets reach the encounter log through the sheet-log save interceptor built in Plan B. The client renders the log through a pure grouping/text class.

**Tech Stack:** .NET 8, ASP.NET Core, EF Core 8 + Npgsql, SignalR (existing `EncounterHub`), Blazor WebAssembly + MudBlazor, xUnit + FluentAssertions, Testcontainers, bUnit.

**Spec:** `docs/superpowers/specs/2026-10-13-versao-1-5-0-c-encontros-design.md` — read it before any task. Where this plan and the spec disagree, the spec wins.

## Global Constraints

- Branch `release/1.5.0`. Commit after every task and `git push`.
- `dotnet build` ends with **0 warnings, 0 errors** (check with `--no-incremental` before committing client work).
- **TDD is mandatory**: failing test first, RED and GREEN evidence in the report.
- Integration tests need Docker. Known timeout flake: a test that fails with a ~100 s timeout and passes when rerun **alone** is the flake.
- Disk is tight: run filtered tests, one `dotnet test` at a time, never the whole integration project in one invocation.
- UI text and `Docs/` are Brazilian Portuguese. MudBlazor icons, never emojis. `Docs/` uses Obsidian `[[wikilinks]]`; requirement IDs are never renumbered.
- Do not bump `AppVersionInfo` or touch `ChangelogDialog` (end of Plan D).
- Never stage the untracked `Docs/harp_notification.mp3`. Stage by path.
- End every commit message with the attribution trailer your own session's system reminder specifies.
- Vocabulary on screen and in `Docs/`: the counter is **Turno** (was "Rodada"); the current participant is **a vez**. Code identifiers `CurrentRound` and `CurrentParticipantId` keep their names.
- The encounter stays GM-only, including its log. No player-facing endpoint or hub message is added.
- A finished encounter is read-only: every mutation except rename and **Reabrir** answers **409** `Encontro finalizado.`
- Nothing is written to the encounter log while the encounter is in `Preparacao` (Turno 0) or `Finalizado`, except the `Iniciado`, `Finalizado` and `Reaberto` events themselves.
- A log row and the change it describes are written in the same `SaveChanges`.

## Review Focus

1. **Encounters that already exist.** After the migration, one that was never advanced is in Turno 0 and one that was is in progress at its round, and both still open and work. Pinned in Task 1.
2. **Removing or losing the participant who has the turn.** The turn moves on instead of pointing at nobody; removing the last participant leaves a usable encounter. Pinned in Tasks 2 and 3.
3. **"Vez anterior" at the edges.** From the first of Turno N it lands on the last of Turno N−1; from the first of Turno 1 it is refused, never Turno 0 or a negative round. Pinned in Tasks 2 and 3.
4. **A sheet in two encounters, or in a finished one.** A resource change on a sheet is logged in every in-progress encounter it takes part in, and in none that is in preparation or finished. Pinned in Task 5.
5. **Double clicks on the turn buttons.** Two fast "Próxima vez" requests must each advance once from the state they find (no skipped participant caused by the page sending a stale target); the buttons are disabled while a request is in flight. Pinned in Tasks 3 and 6.

---

## File map

| File | Responsibility | Task |
|---|---|---|
| `src/RuinaRPG.Domain/Encounters/EncounterStatus.cs`, `EncounterLogTipo.cs` | enums | 1 |
| `src/RuinaRPG.Infrastructure/Encounters/Encounter.cs`, `EncounterLogEntry.cs`, DbContext, migration | schema | 1 |
| `src/RuinaRPG.Domain/Encounters/TurnAdvanceCalculator.cs` | start / next / previous / removal | 2 |
| `src/RuinaRPG.Api/Controllers/EncountersController.cs`, `EncounterParticipantsController.cs` | phases, turn control, removal, 409 | 3 |
| `src/RuinaRPG.Api/Services/EncounterLog.cs`, the two controllers, `GET …/log` | events | 4 |
| `src/RuinaRPG.Infrastructure/SheetLogs/SheetLogInterceptor.cs` (+ a new `EncounterLogDaFicha.cs`) | sheet resources → encounter log | 5 |
| `src/RuinaRPG.Client/Shared/LogDoEncontroAgrupador.cs`, `Pages/GerenciadorDeEncontros.razor` | encounter page | 6 |
| `src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor`, `Docs/Requisitos/*` | two lists, requirements | 7 |

---

### Task 1: Phase and log schema

**Files:**
- Create: `src/RuinaRPG.Domain/Encounters/EncounterStatus.cs`, `src/RuinaRPG.Domain/Encounters/EncounterLogTipo.cs`, `src/RuinaRPG.Infrastructure/Encounters/EncounterLogEntry.cs`
- Modify: `src/RuinaRPG.Infrastructure/Encounters/Encounter.cs`, `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`, `src/RuinaRPG.Contracts/Encounters/EncounterResponse.cs`, `src/RuinaRPG.Api/Controllers/EncountersController.cs` (`Create`, `ToResponseAsync`)
- Create: migration `AddEncounterPhasesAndLog`
- Modify: `Docs/Requisitos/Requisitos - Modelo de Dados.md`
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/EncounterPhasesMigrationTests.cs`, `tests/RuinaRPG.Tests.Integration/Controllers/EncountersControllerTests.cs`

**Interfaces — produces:**

```csharp
namespace RuinaRPG.Domain.Encounters;
public enum EncounterStatus { Preparacao, EmAndamento, Finalizado }
public enum EncounterLogTipo
{
    Iniciado, Finalizado, Reaberto, NovoTurno, VezPassada, VezVoltou, VezEscolhida,
    ParticipanteAdicionado, ParticipanteRemovido, CampoAlterado, CondicaoAdicionada, CondicaoRemovida, RecursoDaFicha
}
```

`Encounter` gains `public EncounterStatus Status { get; set; } = EncounterStatus.Preparacao;` and `public DateTime? FinalizadoEm { get; set; }`; `CurrentRound` now defaults to `0`.

```csharp
namespace RuinaRPG.Infrastructure.Encounters;
/// <summary>Uma linha do log do encontro (Gerenciador de Encontros R0010). Gravada só com o encontro em andamento.</summary>
public class EncounterLogEntry
{
    public Guid Id { get; set; }
    public Guid EncounterId { get; set; }
    public long Sequencia { get; set; }        // identity column: stable order inside one instant
    public DateTime OccurredAt { get; set; }
    public int Turno { get; set; }
    public Guid? VezParticipantId { get; set; } // no FK: the participant may be removed later
    public string? VezNome { get; set; }
    public EncounterLogTipo Tipo { get; set; }
    public string? ParticipanteNome { get; set; }
    public string? Campo { get; set; }
    public string? ValorAntes { get; set; }
    public string? ValorDepois { get; set; }
    public string? Texto { get; set; }
}
```

`DbSet<EncounterLogEntry> EncounterLogEntries`; FK `EncounterId` cascade; `Sequencia` `ValueGeneratedOnAdd()` with `UseIdentityByDefaultColumn()`; index `(EncounterId, Sequencia)`; both enums stored the way the context stores its other enums.

`public record EncounterResponse(string Id, string? Nome, int CurrentRound, int CurrentParticipantIndex, string Status = "Preparacao", DateTime? FinalizadoEm = null, string? CurrentParticipantId = null);`

- [ ] **Step 1: Failing tests.**
  - `EncountersControllerTests`: `Create_starts_in_Preparacao_at_Turno_0_with_nobody_on_turn` (Status "Preparacao", CurrentRound 0, CurrentParticipantId null). Fix the existing tests of this class that asserted `CurrentRound == 1` on a fresh encounter **only where the assertion is about creation**; tests about advancing are Task 3's.
  - `EncounterPhasesMigrationTests` (Review Focus 1): migrate the database to the migration **before** this one (`db.GetService<IMigrator>().MigrateAsync("<previous migration name>")`), insert with raw SQL one encounter with `CurrentParticipantId` NULL and `CurrentRound` 1, and one with a participant row and `CurrentParticipantId` set to it and `CurrentRound` 3; migrate to latest; assert the first is `Preparacao` with `CurrentRound` 0, the second `EmAndamento` with `CurrentRound` 3, both `FinalizadoEm` NULL. Look in `tests/RuinaRPG.Tests.Integration/Persistence` for an existing test that migrates to an intermediate migration and copy its technique; if none exists, use `IMigrator` as above.

- [ ] **Step 2: Run, see them fail.**

- [ ] **Step 3: Enums, entities, configuration, contract, controller.** `Create` needs no explicit assignment (defaults). `ToResponseAsync` returns `e.Status.ToString()`, `e.FinalizadoEm` (as UTC) and `e.CurrentParticipantId?.ToString()`.

- [ ] **Step 4: Migration** — `dotnet ef migrations add AddEncounterPhasesAndLog --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations`. Then edit `Up` so existing rows are converted **after** the columns are added (the column default for `Status` must make new rows `Preparacao`):

```csharp
            migrationBuilder.Sql("""
                UPDATE "Encounters" SET "Status" = 'EmAndamento' WHERE "CurrentParticipantId" IS NOT NULL;
                UPDATE "Encounters" SET "Status" = 'Preparacao', "CurrentRound" = 0 WHERE "CurrentParticipantId" IS NULL;
                """);
```

(Use the literal the enum is stored as: the strings above if enums are strings; the integers 1 and 0 if they are stored as int.) The migration must contain only: the two new `Encounters` columns, the `CurrentRound` default change if EF emits one, the `EncounterLogEntries` table with its FK and index, and this SQL.

- [ ] **Step 5: Run** the two test classes and `--filter FullyQualifiedName~Tests.Integration.Persistence` → PASS.

- [ ] **Step 6: Docs.** `Requisitos - Modelo de Dados.md`: the two new `Encounters` columns (with the three status values and what `CurrentRound` 0 means) and the table `EncounterLogEntries` with every column and the list of `Tipo` values.

- [ ] **Step 7: Commit** — `feat(infra): fases do encontro e tabela do log do encontro`.

---

### Task 2: Turn arithmetic

**Files:**
- Modify: `src/RuinaRPG.Domain/Encounters/TurnAdvanceCalculator.cs`
- Test: `tests/RuinaRPG.Tests.Unit/Encounters/TurnAdvanceCalculatorTests.cs`

**Interfaces — produces** (the existing `Advance` and `IndexOf` stay as they are):

```csharp
    /// <summary>Iniciar: Turno 1, vez do primeiro da ordem. Ordem não pode ser vazia.</summary>
    public static (int Round, Guid ParticipantId) Start(IReadOnlyList<Guid> orderedParticipantIds) => (1, orderedParticipantIds[0]);

    /// <summary>
    /// Vez anterior: o participante anterior na ordem; do primeiro do Turno N volta ao último do
    /// Turno N−1. Nulo quando não há para onde voltar (primeiro do Turno 1, ou a vez atual não está
    /// mais na ordem).
    /// </summary>
    public static (int Round, Guid ParticipantId)? Previous(int currentRound, Guid? currentParticipantId, IReadOnlyList<Guid> orderedParticipantIds)

    /// <summary>
    /// Quem fica com a vez quando o participante que a tinha é removido: o seguinte na ordem (mesmo
    /// Turno) ou, se ele era o último, o primeiro do Turno seguinte. Nulo se não sobra ninguém.
    /// <paramref name="orderedParticipantIds"/> é a ordem ANTES da remoção.
    /// </summary>
    public static (int Round, Guid ParticipantId)? AfterRemoval(int currentRound, Guid removedParticipantId, IReadOnlyList<Guid> orderedParticipantIds)
```

- [ ] **Step 1: Failing tests** (ids `A`, `B`, `C` as fixed Guids, order `[A, B, C]`):

```csharp
    [Fact] public void Start_is_round_1_on_the_first() => TurnAdvanceCalculator.Start(Ordem).Should().Be((1, A));

    [Fact] public void Previous_inside_a_round_steps_back_one() =>
        TurnAdvanceCalculator.Previous(2, C, Ordem).Should().Be((2, B));

    [Fact] public void Previous_from_the_first_of_a_round_goes_to_the_last_of_the_round_before() =>
        TurnAdvanceCalculator.Previous(3, A, Ordem).Should().Be((2, C));

    [Fact] public void Previous_from_the_first_of_round_1_is_refused() =>
        TurnAdvanceCalculator.Previous(1, A, Ordem).Should().BeNull();

    [Fact] public void Previous_when_the_current_participant_is_gone_is_refused() =>
        TurnAdvanceCalculator.Previous(2, Guid.NewGuid(), Ordem).Should().BeNull();

    [Fact] public void Previous_with_nobody_on_turn_is_refused() =>
        TurnAdvanceCalculator.Previous(2, null, Ordem).Should().BeNull();

    [Fact] public void Previous_with_a_single_participant_goes_to_the_round_before() =>
        TurnAdvanceCalculator.Previous(2, A, new[] { A }).Should().Be((1, A));

    [Fact] public void AfterRemoval_of_a_middle_participant_gives_the_turn_to_the_next_in_the_same_round() =>
        TurnAdvanceCalculator.AfterRemoval(2, B, Ordem).Should().Be((2, C));

    [Fact] public void AfterRemoval_of_the_last_starts_the_next_round_on_the_first() =>
        TurnAdvanceCalculator.AfterRemoval(2, C, Ordem).Should().Be((3, A));

    [Fact] public void AfterRemoval_of_the_only_participant_leaves_nobody() =>
        TurnAdvanceCalculator.AfterRemoval(2, A, new[] { A }).Should().BeNull();
```

- [ ] **Step 2: Run, see them fail. Step 3: Implement** (reuse the private `FindIndex`). **Step 4: Run** `dotnet test tests/RuinaRPG.Tests.Unit --filter FullyQualifiedName~TurnAdvanceCalculatorTests` → PASS.

- [ ] **Step 5: Commit** — `feat(domain): iniciar, voltar a vez e remoção do participante da vez`.

---

### Task 3: Phases, turn control and participant removal in the API

**Files:**
- Modify: `src/RuinaRPG.Api/Controllers/EncountersController.cs`, `src/RuinaRPG.Api/Controllers/EncounterParticipantsController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/EncountersControllerTests.cs`, `EncounterParticipantsControllerTests.cs`

**Interfaces — produces** (all GM-only, all 404 for a GM who does not own the campaign, as today):

| Endpoint | Allowed in | Effect |
|---|---|---|
| `POST api/encounters/{id}/start` | Preparacao | needs ≥1 participant (else 400 "Adicione ao menos um participante antes de iniciar o encontro."); `Start`; that participant's `AcoesRestantes = 3`; `Status = EmAndamento` |
| `POST api/encounters/{id}/advance-turn` | EmAndamento | existing behavior |
| `POST api/encounters/{id}/previous-turn` | EmAndamento | `Previous`; null → 400 "Não há vez anterior."; `AcoesRestantes` untouched |
| `POST api/encounters/{id}/turn/{participantId}` | EmAndamento | participant must belong to the encounter (else 404); vez = it; `CurrentRound` unchanged; its `AcoesRestantes = 3` |
| `POST api/encounters/{id}/finish` | EmAndamento | `Status = Finalizado`, `FinalizadoEm = UtcNow` |
| `POST api/encounters/{id}/reopen` | Finalizado | `Status = EmAndamento`, `FinalizadoEm = null` |
| `DELETE api/encounters/{id}/participants/{participantId}` | Preparacao, EmAndamento | removes; if it had the turn, `AfterRemoval` decides the new vez (and round); the participant receiving the turn gets `AcoesRestantes = 3`; null → `CurrentParticipantId = null` |
| `POST …/participants`, `PUT …/participants/{id}` | Preparacao, EmAndamento | existing behavior |
| `PUT api/encounters/{id}` (rename) | all | existing behavior |

Wrong phase: `Finalizado` → **409** "Encontro finalizado."; any other wrong phase → **409** "O encontro não está em andamento." (turn controls and finish in Preparacao), "O encontro já foi iniciado." (start outside Preparacao), "O encontro não está finalizado." (reopen).

- [ ] **Step 1: Failing tests.** In `EncountersControllerTests` (reuse the file's helpers for GM, campaign, encounter and participants; add `StartAsync(encounterId)` and `GetAsync(encounterId)` helpers):
  1. `Start_requires_a_participant` → 400 with the message.
  2. `Start_moves_to_Turno_1_on_the_highest_Iniciativa_and_gives_it_3_actions`.
  3. `Start_twice_is_409`; `Advance_previous_choose_and_finish_are_409_before_start`.
  4. `Advance_walks_the_order_and_wraps_into_the_next_Turno` (three participants; assert vez and `CurrentRound` after each of four calls, and `AcoesRestantes == 3` on whoever receives the turn).
  5. `Previous_steps_back_wraps_to_the_Turno_before_and_is_400_at_the_first_of_Turno_1` (Review Focus 3); assert `AcoesRestantes` of the participant stepped back to is **not** reset (set it to 1 first).
  6. `Choosing_a_participant_gives_it_the_turn_in_the_same_Turno_with_3_actions`; `Choosing_a_participant_of_another_encounter_is_404`.
  7. `Two_advances_in_a_row_each_move_exactly_one_step` (Review Focus 5): send two `advance-turn` requests one after the other without reading state in between; the vez is two steps ahead.
  8. `Finish_makes_everything_but_rename_and_reopen_409` — after finish: add participant, update participant, delete participant, advance, previous, choose, start, finish again → 409 "Encontro finalizado."; rename → 204; `Status == "Finalizado"` and `FinalizadoEm` set.
  9. `Reopen_restores_the_same_Turno_and_vez_and_clears_FinalizadoEm`; `Reopen_before_finish_is_409`.
  10. `A_gm_of_another_campaign_gets_404_on_every_new_endpoint`.
  In `EncounterParticipantsControllerTests`:
  11. `Delete_in_preparation_removes_the_participant`.
  12. `Deleting_the_participant_on_turn_passes_the_turn_on` (Review Focus 2): middle → next, same Turno; last → first, next Turno; the receiver has 3 actions.
  13. `Deleting_the_only_participant_in_progress_leaves_nobody_on_turn_and_the_encounter_usable` — then add a participant and `advance-turn` → 204 and that participant has the turn.
  14. `Delete_of_an_unknown_participant_is_404`.
  Update the existing tests of both classes that advanced a fresh encounter without starting it: start it first. Keep what each asserts.

- [ ] **Step 2: Run, see them fail.**

- [ ] **Step 3: Implement.** Keep the ordering query in one private method in `EncountersController` (`OrderedParticipantsAsync(encounterId)` → `Iniciativa` desc, then `Id`), used by start/advance/previous. A private `ActionResult? RequirePhase(Encounter e, params EncounterStatus[] allowed)` produces the 409s above; `EncounterParticipantsController` gets the same guard (duplicate the small method rather than introducing a shared base class).

  `advance-turn` when `CurrentParticipantId` is null in `EmAndamento` (after the only participant was removed): the next is the first of the order in the **same** Turno. `TurnAdvanceCalculator.Advance` treats null as "the first already has the turn" and would return the second; so for null call `(encounter.CurrentRound, ordered[0])` directly. Test 13 pins this.

  `Delete`: compute `AfterRemoval` with the order **before** removal, apply it, then remove the row and its conditions in the same save.

- [ ] **Step 4: Run** both classes → PASS. `dotnet build` → 0 warnings.

- [ ] **Step 5: Commit** — `feat(api): fases do encontro, controle de vez e remoção de participante`.

---

### Task 4: Encounter events and the log endpoint

**Files:**
- Create: `src/RuinaRPG.Api/Services/EncounterLog.cs`, `src/RuinaRPG.Contracts/Encounters/EncounterLogEntryResponse.cs`
- Modify: `src/RuinaRPG.Api/Program.cs` (register scoped), the two encounter controllers
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/EncounterLogTests.cs`

**Interfaces — produces:**

```csharp
/// <summary>Monta as linhas do log do encontro; só grava com o encontro em andamento. Quem chama salva.</summary>
public class EncounterLog(RuinaRpgDbContext db)
{
    /// <summary>No-op unless encounter.Status == EmAndamento. Snapshots Turno and whose turn it is from the encounter as it is NOW.</summary>
    public Task RegistrarAsync(Encounter encounter, EncounterLogTipo tipo, string? participanteNome = null,
        string? campo = null, string? valorAntes = null, string? valorDepois = null, string? texto = null);
}

public record EncounterLogEntryResponse(long Sequencia, DateTime OccurredAt, int Turno, string? VezNome, string Tipo,
    string? ParticipanteNome, string? Campo, string? ValorAntes, string? ValorDepois, string? Texto);
```

`GET api/encounters/{id}/log` → `List<EncounterLogEntryResponse>` ordered by `Sequencia`, GM of the campaign only (404 otherwise).

Events, each added before the controller's `SaveChangesAsync`:

| Action | Rows, in this order |
|---|---|
| start | `Iniciado` (after `Status` is set, so it is recorded); `VezPassada` (ParticipanteNome = who has the turn) |
| advance | `NovoTurno` when `CurrentRound` increased; `VezPassada` |
| previous | `VezVoltou` (ParticipanteNome = who now has the turn) |
| choose | `VezEscolhida` |
| finish | `Finalizado` — registered **before** `Status` changes |
| reopen | `Reaberto` — registered **after** `Status` changes |
| add participant (in progress) | `ParticipanteAdicionado` |
| delete participant (in progress) | `ParticipanteRemovido`; then `NovoTurno` if the round increased; then `VezPassada` if the turn moved to someone |
| update participant (in progress) | one `CampoAlterado` per changed field among Iniciativa, Ações, PV, PF, PA (Campo = "Iniciativa", "Ações", "PV", "PF", "PA"; values as text, null → "—"); one `CondicaoAdicionada` per text in the request not present before; one `CondicaoRemovida` per text present before and absent now (compare case-sensitively, as stored) |

`RegistrarAsync` sets `Turno = encounter.CurrentRound`, `VezParticipantId = encounter.CurrentParticipantId` and `VezNome` = that participant's `Nome` (from the tracked entity if present, else one query), `OccurredAt = DateTime.UtcNow`. So call it **after** the encounter fields are updated for the action, except `Finalizado`.

- [ ] **Step 1: Failing tests** (`EncounterLogTests`, real API calls, then `GET …/log`):
  1. `Nothing_is_logged_in_preparation` — add, edit, remove participants at Turno 0: empty log.
  2. `Start_logs_Iniciado_then_the_first_turn`.
  3. `Advancing_through_a_wrap_logs_NovoTurno_before_the_turn` — the `NovoTurno` row has the new `Turno`.
  4. `Previous_and_choose_log_their_own_types`.
  5. `Editing_a_participant_logs_each_changed_field_with_before_and_after_and_nothing_for_unchanged_ones` — change Iniciativa and PV of a GM-owned NPC instance; resend the same values: no new rows.
  6. `Conditions_log_additions_and_removals_only` — from ["Envenenado"] to ["Envenenado", "Enraizado"] logs one `CondicaoAdicionada` "Enraizado"; to ["Enraizado"] logs one `CondicaoRemovida` "Envenenado".
  7. `Adding_and_removing_a_participant_in_progress_is_logged_and_removing_the_one_on_turn_logs_the_new_turn`.
  8. `Finish_and_reopen_are_logged_and_nothing_else_is_while_finished` — finish; try a mutation (409); reopen; the log ends with `Finalizado`, `Reaberto`.
  9. `The_log_is_ordered_by_Sequencia_and_each_row_carries_Turno_and_whose_turn_it_was`.
  10. `The_log_is_404_for_another_gm`.

- [ ] **Step 2: Run, see them fail. Step 3: Implement.** In `Update`, read the participant's old Iniciativa/Ações/PV/PF/PA and old condition texts before overwriting. **Step 4: Run** the class plus `EncountersControllerTests` and `EncounterParticipantsControllerTests` → PASS.

- [ ] **Step 5: Commit** — `feat(api): log do encontro com os eventos do GM`.

---

### Task 5: Sheet resource changes in the encounter log

**Files:**
- Modify: `src/RuinaRPG.Infrastructure/SheetLogs/SheetLogInterceptor.cs`
- Create: `src/RuinaRPG.Infrastructure/SheetLogs/EncounterLogDaFicha.cs`
- Test: `tests/RuinaRPG.Tests.Integration/SheetLogs/EncounterLogDaFichaTests.cs`

**Interfaces:**
- Consumes: the `SheetLogEntry` rows the interceptor builds for one save (Plan B), `EncounterLogEntry` (Task 1).
- Produces: for every built `SheetLogEntry` with `Categoria == RecursoVFA` and `Acao == Alterou`, one `EncounterLogEntry` (`Tipo = RecursoDaFicha`, `ParticipanteNome` = the participant's `Nome`, `Campo`/`ValorAntes`/`ValorDepois` copied, `Turno` and vez snapshot from the encounter) for **each** participant linked to that sheet (`SourceCharacterSheetId` / `SourceNpcSheetId` / `SourceCreatureSheetId`) whose encounter has `Status == EmAndamento`. Added to the same save.

- [ ] **Step 1: Failing tests** (real API: a player edits their sheet's Vitalidade while taking part in encounters; read `GET …/log` as the GM):
  1. `A_Vitalidade_change_on_a_sheet_in_an_in_progress_encounter_is_logged_there` — `Tipo == "RecursoDaFicha"`, the participant's name, the label the sheet log uses, before and after.
  2. `Foco_and_Adrenalina_are_logged_too_and_Estresse_and_other_fields_are_not`.
  3. `Nothing_is_logged_at_Turno_0_or_after_finish_and_logging_resumes_after_reopen` (Review Focus 4).
  4. `A_sheet_in_two_in_progress_encounters_is_logged_in_both` (Review Focus 4).
  5. `A_granted_npc_sheet_edited_by_its_player_is_logged` and `a_gm_owned_npc_instance_is_not_affected_by_edits_to_its_source_sheet` (the GM's NPC sheet is not logged at all by Plan B, so no row appears).
  6. `The_GM_editing_the_players_sheet_is_logged_too`.
  7. `A_sheet_that_is_in_no_encounter_adds_no_query_result_and_no_row` — assert no `EncounterLogEntries` rows exist for any encounter after the edit.

- [ ] **Step 2: Run, see them fail.**

- [ ] **Step 3: Implement.** First split the interceptor's row-building method into named private methods without changing behavior (Plan B's final review asked for this before extending it): collecting the tracked sheet entries, resolving sheet/grant, building the rows, the effects pass. Run `--filter FullyQualifiedName~Tests.Integration.SheetLogs` to prove nothing moved.

  Then `EncounterLogDaFicha` (static or a small class used by the interceptor): given the built rows and the context, take the distinct sheets that have a `RecursoVFA`/`Alterou` row; with **one** query load `(participant Id, Nome, encounter Id, CurrentRound, CurrentParticipantId, source sheet ids)` for participants of those sheets in `EmAndamento` encounters (`AsNoTracking`); when the result is empty, return without further work; otherwise resolve the vez names with one more query and build the `EncounterLogEntry` rows. It runs inside the same try/catch guard as the sheet rows (a fault here must not fail the save), and its rows are added and, on a failed save, detached together with the sheet rows.

- [ ] **Step 4: Run** the new class, `--filter FullyQualifiedName~Tests.Integration.SheetLogs`, `EncounterLogTests`, `CharacterSheetsControllerTests` → PASS. `dotnet build` → 0 warnings.

- [ ] **Step 5: Docs.** `Requisitos - Técnico.md` R0012: one sentence — the same interceptor also writes a line in the log of every in-progress encounter the ficha takes part in when its Vitalidade, Foco or Adrenalina atual changes (ver "[[Requisitos - Gerenciador de Encontros]]").

- [ ] **Step 6: Commit** — `feat(api): mudanças de recurso na ficha entram no log do encontro em andamento`.

---

### Task 6: The encounter page

**Files:**
- Create: `src/RuinaRPG.Client/Shared/LogDoEncontroAgrupador.cs`
- Modify: `src/RuinaRPG.Client/Pages/GerenciadorDeEncontros.razor`
- Test: `tests/RuinaRPG.Tests.Client/Shared/LogDoEncontroAgrupadorTests.cs`, `tests/RuinaRPG.Tests.Client/Pages/GerenciadorDeEncontrosTests.cs` (new)

**Interfaces — produces:**

```csharp
public static class LogDoEncontroAgrupador
{
    /// <summary>Grupos por Turno (ordem crescente) e, dentro dele, por vez, na ordem em que aconteceram.</summary>
    public static List<TurnoDoLog> PorTurno(IEnumerable<EncounterLogEntryResponse> linhas);
    public static string Texto(EncounterLogEntryResponse linha);
}
public sealed record TurnoDoLog(int Turno, List<VezDoLog> Vezes);
public sealed record VezDoLog(string? VezNome, List<EncounterLogEntryResponse> Linhas);
```

`Texto`:

| Tipo | Text |
|---|---|
| Iniciado / Finalizado / Reaberto | "Encontro iniciado" / "Encontro finalizado" / "Encontro reaberto" |
| NovoTurno | "Início do Turno {Turno}" |
| VezPassada | "Vez de {ParticipanteNome}" |
| VezVoltou | "A vez voltou para {ParticipanteNome}" |
| VezEscolhida | "A vez foi passada para {ParticipanteNome}" |
| ParticipanteAdicionado / Removido | "{ParticipanteNome} entrou no encontro" / "{ParticipanteNome} saiu do encontro" |
| CampoAlterado | "{ParticipanteNome} — {Campo}: {ValorAntes} → {ValorDepois}" |
| CondicaoAdicionada / Removida | "{ParticipanteNome} recebeu a condição {Texto}" / "{ParticipanteNome} perdeu a condição {Texto}" |
| RecursoDaFicha | "{ParticipanteNome} — {Campo}: {ValorAntes} → {ValorDepois} (na ficha)" |
| anything else | the `Tipo` string itself |

A new `VezDoLog` group starts whenever `VezNome` differs from the previous line's inside the same Turno.

- [ ] **Step 1: Failing unit tests** for `PorTurno` (two Turnos; inside one, lines of A, then B, then A again → three vez groups in that order; rows with null `VezNome` form their own group) and for every row of the `Texto` table.

- [ ] **Step 2: Implement** → PASS.

- [ ] **Step 3: Failing page tests** (`GerenciadorDeEncontrosTests`; stub `campaigns`, `campaigns/{id}/encounters`, `encounters/{id}/participants`, `encounters/{id}/log`; the page builds a SignalR connection in `OnInitializedAsync` — look at how `MudBunitContext` and existing page tests deal with hub connections, and if the page cannot be rendered under bUnit because of it, extract the connection behind the smallest interface that the existing `INotificationConnection`/`FakeNotificationConnection` pattern suggests, and say so in the report):
  1. Preparation: header shows "Turno 0" and the chip "Preparação"; "Iniciar encontro" is present; "Próxima vez", "Vez anterior" and "Finalizar encontro" are absent; each row has a remove button and no "passar a vez" button; the log section is absent or empty-stated.
  2. In progress: the bar reads "Turno 2, vez de Goblin"; the row of the participant on turn carries the CSS class `encontro-vez` and a PlayArrow icon; the three turn buttons are present; each other row has a button with `aria-label="Passar a vez para este"`.
  3. Clicking "Próxima vez" posts `advance-turn`, and while the request is pending the three turn buttons are disabled (Review Focus 5); "Vez anterior" posts `previous-turn`; the row button posts `turn/{id}`.
  4. "Finalizar encontro" asks for confirmation before posting `finish`.
  5. Finished: chip "Finalizado"; no add form, no edit/remove/turn buttons; "Reabrir" present and posts `reopen`; the log is shown.
  6. The log renders one heading per Turno ("Turno 1"), the vez sub-headings, and each line's text and local time.
  7. An API error on a turn button shows the API's message in the page alert.

- [ ] **Step 4: Implement the page.**
  - Header: `{Nome} — Turno {CurrentRound}` plus a `MudChip` ("Preparação" default color, "Em andamento" success, "Finalizado" dark). Replace every on-screen "Rodada" with "Turno".
  - Bar (in progress only): `MudAlert Severity.Info`, icon `Icons.Material.Filled.PlayArrow`, "Turno {n}, vez de {nome}" (or "Turno {n}, ninguém com a vez" when `CurrentParticipantId` is null).
  - Buttons by phase, in one row: Preparação → "Iniciar encontro"; Em andamento → "Vez anterior" (`Icons.Material.Filled.SkipPrevious`), "Próxima vez" (`SkipNext`, primary), "Finalizar encontro" (`Flag`, outlined, opens a `MudMessageBox` "Finalizar o encontro? Ele passa a ser somente leitura; é possível reabrir depois."); Finalizado → "Reabrir" (`Replay`). All disabled while `_enviando`.
  - Rows: identify the current participant by `_encounter.CurrentParticipantId == participant.Id` (not by index); class `encontro-vez` (filled background from the theme's primary lighten, in a `.razor.css` file next to the page) and a leading PlayArrow icon. Per row: "Passar a vez para este" icon button (`Icons.Material.Filled.Redo`, in progress, not on the current row), "Editar" (existing), remove icon button (`Icons.Material.Filled.PersonRemove`, with a confirmation `MudMessageBox`), none of them when finished.
  - The add-participant section is not rendered when finished.
  - Error handling: on a non-success response show `await response.Content.ReadErrorMessageAsync()` (the existing extension) with the current generic text as fallback.
  - Section "Log do encontro" under the table (not in preparation): `LogDoEncontroAgrupador.PorTurno`, each Turno as a `MudText Typo.h6`, each vez as `Typo.subtitle2` ("Vez de {nome}", or nothing for the null group), each line `HH:mm` local + `Texto`. Loaded in `LoadAsync` together with the rest, so the existing `ParticipantsChanged` hub handler refreshes it too.

- [ ] **Step 5: Run** `dotnet build --no-incremental` (0 warnings) and `dotnet test tests/RuinaRPG.Tests.Client` → PASS.

- [ ] **Step 6: Commit** — `feat(client): encontro com fases, controle de vez e log`.

---

### Task 7: Two lists in the campaign and the requirements

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor` (tab "Encontros", ~line 126-138)
- Modify: `Docs/Requisitos/Requisitos - Gerenciador de Encontros.md`, `Docs/Requisitos/Requisitos - Campanha.md`
- Test: `tests/RuinaRPG.Tests.Client/Pages/CampanhaDetalheTests.cs`

- [ ] **Step 1: Failing page tests:** with three encounters stubbed (Preparacao, EmAndamento at round 4, Finalizado with `FinalizadoEm`), the section "Encontros" lists the first two with the chips "Preparação" and "Turno 4" and not the third; the section "Encontros finalizados" lists only the third with its finish date `dd/MM/yyyy`; with no finished encounter the second section shows "Nenhum encontro finalizado."; finished ones are ordered by `FinalizadoEm` descending.

- [ ] **Step 2: Implement.** Split `_encounters` by `Status` in the markup; keep the links.

- [ ] **Step 3: Docs — `Requisitos - Gerenciador de Encontros.md`.** Do not renumber. Amend and add:
  - **R0001**: replace "um de cada vez ativo ou não" with a sentence saying each Encontro is in one of three fases (ver R0007).
  - **R0005/R0006**: replace "Próximo turno" by "Próxima vez" and "Rodada" by "Turno" throughout; R0006 gains: "Vez anterior" volta um passo, inclusive do primeiro do Turno N para o último do Turno N−1, sem alterar as Ações restantes e sem voltar antes do início do Turno 1; "Passar a vez para este", em cada participante, dá a vez fora da ordem, mantendo o Turno e voltando as Ações dele para 3. A tela mostra sempre, em destaque, o Turno e de quem é a vez.
  - **R0007** `# **R0007** - O Encontro passa por três fases.` — Turno 0 (preparação: só montar participantes e Iniciativas; nada é registrado no log), em andamento (a partir de "Iniciar encontro", que exige ao menos um participante, leva ao Turno 1 e dá a vez ao primeiro da ordem) e finalizado ("Finalizar encontro", com confirmação; somente leitura; "Reabrir" devolve ao mesmo Turno e à mesma vez). Encontros criados antes dessa regra: os que nunca tiveram a vez avançada ficam no Turno 0; os demais, em andamento.
  - **R0008** `# **R0008** - O GM pode remover um participante.` — na preparação e em andamento; se ele tinha a vez, ela passa ao seguinte (ou ao primeiro do Turno seguinte, se era o último).
  - **R0009** `# **R0009** - Encontros finalizados ficam em uma lista à parte.` — em "[[Requisitos - Campanha]]", a aba Encontros mostra "Encontros" (em preparação e em andamento, com a fase ou o Turno) e "Encontros finalizados" (com a data de finalização, do mais recente para o mais antigo).
  - **R0010** `# **R0010** - O Encontro mantém um log a partir do Turno 1.` — o que é registrado (início, cada novo Turno, cada passagem de vez inclusive voltar e escolher, participante adicionado ou removido, alterações de Iniciativa, Ações, PV/PF/PA e Condições, finalização e reabertura), que as alterações de Vitalidade, Foco e Adrenalina feitas na ficha de um participante ligado a uma ficha também entram enquanto o encontro está em andamento, que o log aparece na própria tela agrupado por Turno e por vez, e que é restrito ao GM como o resto do Encontro.
  - `Requisitos - Campanha.md`: where the tab Encontros / the encounter list is described, point to R0009 of the Gerenciador.

- [ ] **Step 4: Verify** — `dotnet build --no-incremental` (0 warnings), `dotnet test tests/RuinaRPG.Tests.Unit`, `dotnet test tests/RuinaRPG.Tests.Client`.

- [ ] **Step 5: Commit and push** — `feat(client): lista de encontros finalizados e requisitos das fases do encontro`.
