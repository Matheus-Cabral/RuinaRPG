# Versão 1.5.0 — Plano D: Condições e Resumo da Ficha no Encontro

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the free-text conditions of an encounter participant by catalog Condições kept by the Auditor, whose modifiers and disables change a sheet only while the condition and the encounter are active; show them to the player; give the GM a summary popup of an NPC/Criatura participant; and ship version 1.5.0.

**Architecture:** A condition applied to a participant is an independent snapshot (jsonb). One pure Domain engine (`EfeitosDeCondicao`) turns the active snapshots of a sheet into adjustments; the three `*SheetStats` services feed it into the modifier path that already carries Artefato bonuses and equipment penalties, so sheet endpoints change in one place per kind. Everything that happens on a turn change goes through one hand-over method in the API, which is where per-turn adjustments and expiry hook in. Non-granted NPC/Criatura sheets are never touched: their conditions are applied only by the summary endpoint.

**Tech Stack:** .NET 8, ASP.NET Core, EF Core 8 + Npgsql (jsonb), SignalR (`EncounterHub`, `NotificationHub`), Blazor WebAssembly + MudBlazor, xUnit + FluentAssertions, Testcontainers, bUnit.

**Spec:** `docs/superpowers/archive/specs/2026-10-13-versao-1-5-0-d-condicoes-e-resumo-design.md` — read it in full before any task, including its two "Carried over" sections. Where this plan and the spec disagree, the spec wins.

## Global Constraints

- Branch `release/1.5.0`. Commit after every task and `git push`.
- `dotnet build --no-incremental` ends with **0 warnings, 0 errors** before every commit.
- **TDD is mandatory**: failing test first, RED and GREEN evidence in the report.
- Integration tests need Docker. Known timeout flake: a test that fails with a ~100 s timeout and passes when rerun **alone** is the flake. Disk is tight: filtered runs, one `dotnet test` at a time, never the whole integration project in one invocation.
- UI text and `Docs/` are Brazilian Portuguese. MudBlazor icons, never emojis. `Docs/` uses Obsidian `[[wikilinks]]`; requirement IDs are never renumbered.
- Never stage the untracked `Docs/harp_notification.mp3`. Stage by path. End every commit message with the attribution trailer your own session's system reminder specifies.
- `AppVersionInfo` and `ChangelogDialog` are touched only in Task 13.
- The name is **Condições**. The existing page **Auditoria → Efeitos** (effects a Magia buys) is another catalog and is not renamed or changed.
- **Where a condition acts** (the rule every task must respect):

  | Participant | Effect on the sheet | Who sees it |
  |---|---|---|
  | Ficha de Personagem | the sheet's computed values change while active | player and GM |
  | NPC/Criatura granted to a player (`OwnerId` set) | same as Personagem | owner and GM |
  | NPC/Criatura of the GM (not granted) | **none**; only the encounter summary popup applies it | GM |

- A condition is **active** only while it is on the participant, has not expired, and the participant's encounter is `EmAndamento`. Preparation and finished encounters have no active conditions; reopening reactivates.
- Total modifiers are temporary. Current-value adjustments (`RecursoAtual`) are real changes and are never given back.
- Budgets, level limits and Passiva requirement checks ignore conditions.
- The same condition (same `CondicaoId`) cannot be on one participant twice. Different conditions add up.
- Percentage: applied over (base + all fixed modifiers), rounded down; a percentage on a current value is a percentage of that resource's total.
- Conditions, the catalog page excepted, are GM-driven: no player endpoint applies or removes one.

## Review Focus

1. **A condition leaks out of its encounter.** After removal, expiry, finish, or deletion of the participant, every computed value of the sheet is back to exactly what it was; nothing computed with a condition is ever stored. Pinned in Tasks 6, 7 and 8.
2. **A non-granted NPC sheet is changed.** The GM's own NPC/Criatura sheet endpoints never reflect a condition, with three instances of one sheet carrying different conditions. Pinned in Tasks 6 and 11.
3. **Going back and forward re-applies effects.** "Vez anterior" then "Próxima vez" must not repeat a per-turn adjustment for the same participant and Turno, nor re-expire. Pinned in Task 9.
4. **Stale player screen.** A player with the sheet open sees the condition appear and disappear without reloading, and an autosave from that open page does not store condition-modified values. Pinned in Tasks 6 and 10.
5. **Percent and fixed on dependent values.** A −50% on an Atributo changes the Perícias and Sub-atributos that depend on it once, not twice, and a percent never turns a total negative. Pinned in Tasks 5 and 6.

---

### Task 1: One place for turn hand-over, and a concurrency token on `Encounter`

Behavior-preserving refactor plus one new behavior (409 on a concurrent turn change).

**Files:** `src/RuinaRPG.Api/Services/EncounterTurnos.cs` (new, scoped), `EncountersController.cs`, `EncounterParticipantsController.cs`, `Program.cs`, `Encounter.cs` + DbContext + migration `AddEncounterRowVersion`, tests in `EncountersControllerTests.cs`.

**Interfaces — produces:**

```csharp
public enum MotivoDaVez { Inicio, Proxima, Anterior, Escolhida, Remocao }

public class EncounterTurnos(RuinaRpgDbContext db, EncounterLog log)
{
    /// <summary>
    /// Único ponto em que a vez muda. Ajusta CurrentRound/CurrentParticipantId, devolve as Ações a 3
    /// quando o motivo dá a vez (todos menos Anterior), e registra NovoTurno/VezPassada/VezVoltou/
    /// VezEscolhida como hoje. Quem chama salva. Devolve o que aconteceu, para os ganchos das Condições.
    /// </summary>
    public Task<MudancaDeVez> DarAVezAsync(Encounter encounter, EncounterParticipant? participante, int round, MotivoDaVez motivo);
}
public sealed record MudancaDeVez(int RoundAnterior, int RoundNovo, Guid? ParticipanteAnteriorId, EncounterParticipant? Participante, MotivoDaVez Motivo)
{
    public bool EntrouEmNovoTurno => RoundNovo > RoundAnterior;       // Inicio (0 → 1) included
    public bool ParticipanteRecebeuAVez => Participante is not null && Motivo != MotivoDaVez.Anterior;
}
```

`Encounter` gets a concurrency token mapped to PostgreSQL `xmin` (`UseXminAsConcurrencyToken()` or the Npgsql 8 equivalent `IsRowVersion()` on a `uint xmin` shadow/CLR property — follow Npgsql's documented way for the installed version). A `DbUpdateConcurrencyException` on a turn/phase action answers **409** "O encontro foi alterado por outra ação. Recarregue e tente de novo."

- [ ] Tests first: (a) the whole existing `EncountersControllerTests`, `EncounterParticipantsControllerTests` and `EncounterLogTests` must pass unchanged after the refactor (they are the safety net; run them before and after); (b) new: two contexts load the same in-progress encounter, both advance, the second `SaveChanges` → 409 through the API (simulate with two requests racing via `Task.WhenAll`, asserting exactly one 204 and one 409, and the turn advanced exactly one step); (c) a unit-level test of `MudancaDeVez` flags for each `MotivoDaVez`, with `Inicio` reporting `EntrouEmNovoTurno`.
- [ ] Route `Start`, `AdvanceTurn`, `PreviousTurn`, `ChooseTurn` and the hand-over branch of participant `Delete` through `DarAVezAsync`. No log text, order or status code changes.
- [ ] Docs: `Requisitos - Modelo de Dados.md` (the token). Commit — `refactor(api): um único ponto para a mudança de vez e controle de concorrência no encontro`.

---

### Task 2: Catalog schema, validation and API

**Files:** Domain `src/RuinaRPG.Domain/Condicoes/` (enums, `CondicaoValidator.cs`), Infrastructure `src/RuinaRPG.Infrastructure/Condicoes/` (`Condicao.cs`, `CondicaoModificador.cs`, `CondicaoDesabilitacao.cs`), DbContext, migration `AddCondicoes`, Contracts `src/RuinaRPG.Contracts/Condicoes/`, `src/RuinaRPG.Api/Controllers/CondicoesController.cs`, tests `tests/RuinaRPG.Tests.Unit/Condicoes/CondicaoValidatorTests.cs`, `tests/RuinaRPG.Tests.Integration/Controllers/CondicoesControllerTests.cs`, migration test.

**Interfaces — produces:**

```csharp
namespace RuinaRPG.Domain.Condicoes;
public enum AlvoDeCondicao { Atributo, Pericia, SubAtributo, RecursoTotal, RecursoAtual, AfinidadeElemental, DanoElemental, Dano, Maestria }
public enum TipoDeValorDeCondicao { Fixo, Percentual }
public enum MomentoDeCondicao { AoAplicar, ACadaVez }
public enum CategoriaDesabilitavel { Arma, Armadura, Escudo, Artefato, Magia, Habilidade, Runa, Passiva }
public enum RecursoDeCondicao { Vitalidade, Foco, Adrenalina, Estresse }

public sealed record ModificadorDeCondicao(AlvoDeCondicao Alvo, string? AlvoEspecifico, TipoDeValorDeCondicao TipoDeValor, int Valor, MomentoDeCondicao? Momento);

public static class CondicaoValidator
{
    /// <summary>Null when valid; otherwise the Portuguese message for the first problem.</summary>
    public static string? Validar(string nome, IReadOnlyList<ModificadorDeCondicao> modificadores, IReadOnlyList<CategoriaDesabilitavel> desabilitacoes, int? duracaoPadraoTurnos, Func<string, bool> periciaExiste);
}
```

Validation rules: `Nome` non-blank; at least one modifier or one disable; `Momento` set **iff** `Alvo == RecursoAtual`; `Valor != 0`; `Percentual` between −100 and 1000; `DuracaoPadraoTurnos` null or ≥ 1; no duplicate disable category; `AlvoEspecifico` per `Alvo`:

| Alvo | AlvoEspecifico |
|---|---|
| Atributo | required: an `Atributo` enum name |
| Pericia | null (all) or an existing Perícia key |
| SubAtributo | required: one of the `SubAtributoAlvo` constants |
| RecursoTotal, RecursoAtual | required: a `RecursoDeCondicao` name |
| AfinidadeElemental, DanoElemental | null (all) or an `Elemento` name |
| Dano | null (all) or a `TipoDeDano` name |
| Maestria | null (all) or an existing Perícia key |

Tables: `Condicoes` (`Id`, `Nome`, `Descricao`, `Icone`, `DuracaoPadraoTurnos`, `IsDeleted`, `UpdatedByUserId`, `UpdatedAt`; unique index on `Nome` filtered to `IsDeleted = false`), `CondicaoModificadores` (FK cascade, `Ordem`, the five fields), `CondicaoDesabilitacoes` (FK cascade, `Categoria`). Enums stored as strings.

API `api/condicoes`, modelled on `CreatureExclusiveTraitsController` (same Auditor check): `GET` (any authenticated user; `?nome=`; not deleted; each with its modifiers and disables), `POST`, `PUT {id}`, `DELETE {id}` (soft; always allowed). `Icone` must be one of a fixed list of keys declared once in Contracts (`CondicaoIcones.Chaves`, ~20 names such as "Veneno", "Fogo", "Gelo", "Atordoado", "Sangramento", "Escudo", "Lentidao", "Cegueira", "Medo", "Bencao"...), which the client maps to MudBlazor icons in Task 3.

- [ ] Unit tests: one per validation rule above, plus a fully valid multi-line condition.
- [ ] Integration tests: CRUD round-trip with two modifiers and two disables; non-Auditor gets 403 on writes and 200 on GET; duplicate name → 400; name reusable after soft delete; invalid payload → 400 with the validator's message; migration test for the three tables.
- [ ] Docs: `Requisitos - Modelo de Dados.md` (three tables, the enums). Commit — `feat(api): catálogo de Condições do Auditor`.

---

### Task 3: Auditoria → Condições page

**Files:** `src/RuinaRPG.Client/Pages/AuditoriaCondicoes.razor`, `src/RuinaRPG.Client/Shared/CondicaoIcone.cs` (key → MudBlazor icon), `src/RuinaRPG.Client/Shared/Fields/CondicaoModificadoresEditor.razor`, `Layout/RulesAuditorNavLinks.razor`, tests `tests/RuinaRPG.Tests.Client/Pages/AuditoriaCondicoesTests.cs`, docs `Requisitos - Auditoria de Regras.md` (new requirement, next free number).

Page at `/auditoria/condicoes`, nav link "Condições" (`Icons.Material.Filled.Coronavirus`) placed after "Efeitos". Same shape as the other auditoria pages (forbidden alert for a non-Auditor; add form; list; edit; delete with confirmation). The form: Nome, Descrição (multi-line), icon picker (the fixed keys rendered as icons), Duração padrão (Turnos, optional), an editable table of modifier lines — Alvo select → a dependent "específico" select whose options come from the table in Task 2 (Perícias from `PericiaCatalogo`; "Todos"/"Todas" for the null choice) → Fixo/Percentual → Valor → Momento, shown only for "Valor atual de um recurso" — and a multi-select of the eight disable categories with Portuguese labels. An `InfoPopup` at the top explains in plain Portuguese what a Condição is, when it acts (only during an encounter em andamento) and the difference between total and atual. API errors show the API's message.

- [ ] bUnit tests: forbidden state; the dependent select changes with Alvo; Momento only for RecursoAtual; adding two lines and saving posts the expected body; a 400 shows the message; the nav link only for the Auditor.
- [ ] Docs: the new Auditoria de Regras requirement describing the catalog (global, Auditor-only writes, fields, the list of what a modifier can target, the disable categories, that a catalog edit does not change a condition already applied). Commit — `feat(client): Auditoria de Condições`.

---

### Task 4: Conditions applied to a participant

**Files:** `src/RuinaRPG.Infrastructure/Encounters/EncounterParticipantCondicao.cs`, DbContext, migration `AddEncounterParticipantCondicoes`, `EncounterLogTipo` (+ `CondicaoExpirada`, `AjusteDeCondicao`), Contracts (`AplicarCondicaoRequest`, `CondicaoAplicadaResponse`, `ItensDesabilitaveisResponse`), `EncounterParticipantCondicoesController.cs` (new), `EncounterParticipantsController.cs` (`ToResponseAsync`), tests `EncounterParticipantCondicoesControllerTests.cs`.

`EncounterParticipantCondicao`: `Id`, `EncounterParticipantId` (FK cascade), `CondicaoId` (nullable, no FK), `Nome`, `Descricao`, `Icone`, `AplicadaNoTurno`, `DuracaoTurnos?`, `Modificadores` (jsonb: the lines with the values the GM adjusted, each with a stable `LinhaId`), `Desabilitacoes` (jsonb: per category, a list of sheet-row ids; empty list = whole category), `AjustesAplicados` (jsonb: list of `{LinhaId, Turno}` already applied — used in Task 9).

Endpoints (GM of the campaign only; 409 when the encounter is finished):
- `POST api/encounters/{id}/participants/{pid}/condicoes` — body `{ CondicaoId, DuracaoTurnos?, ValoresAjustados?: {linha index → valor}, ItensDesabilitados?: {categoria → [ids]} }`. Copies the catalog entry into a snapshot; `AplicadaNoTurno = CurrentRound`; duration defaults to the catalog's. 400 "Esta condição já está neste participante." on a duplicate `CondicaoId`; 400 for an unknown/deleted condition, an adjusted value of 0, or an item id that is not a row of that participant's sheet in that category.
- `DELETE …/condicoes/{condicaoAplicadaId}`.
- `GET …/participants/{pid}/itens-desabilitaveis` — for each of the eight categories, the participant's sheet rows `(Id, Nome)`; empty for a participant whose source sheet is gone.

`EncounterParticipantResponse` gains `List<CondicaoAplicadaResponse> CondicoesAplicadas` (`Id`, `Nome`, `Descricao`, `Icone`, `TurnosRestantes?`, `Ativa`) next to the legacy `Condicoes` list of texts, which stays and can only be reduced from now on: `UpdateParticipantRequest.Condicoes` may remove legacy texts but a text not already present is refused (400 "Novas condições vêm do catálogo.").

Log (through `EncounterLog`, in progress only): `CondicaoAdicionada` / `CondicaoRemovida` with `Texto` = the condition name.

- [ ] Integration tests for every rule above, including: the snapshot is unchanged after the catalog entry is edited and after it is soft-deleted; conditions can be applied in preparation (kept, `Ativa == false`); two instances of one NPC sheet hold different conditions; `TurnosRestantes` computed as `max(AplicadaNoTurno,1) + DuracaoTurnos − CurrentRound`; migration test.
- [ ] Docs: Modelo de Dados (table, the independent-copy pattern from the legend). Commit — `feat(api): Condições do catálogo aplicadas a participantes do encontro`.

---

### Task 5: The engine (pure Domain)

**Files:** `src/RuinaRPG.Domain/Condicoes/EfeitosDeCondicao.cs`, `CondicaoAtiva.cs`, tests `tests/RuinaRPG.Tests.Unit/Condicoes/EfeitosDeCondicaoTests.cs`.

**Interfaces — produces:**

```csharp
public sealed record CondicaoAtiva(Guid Id, string Nome, IReadOnlyList<ModificadorDeCondicao> Modificadores,
    IReadOnlyDictionary<CategoriaDesabilitavel, IReadOnlySet<Guid>> Desabilitacoes); // empty set = whole category

public sealed class EfeitosDeCondicao
{
    public static readonly EfeitosDeCondicao Nenhum;
    public EfeitosDeCondicao(IReadOnlyList<CondicaoAtiva> ativas);
    public bool Vazio { get; }
    /// <summary>valorBase + fixed lines, then the sum of percentage lines over that, rounded down. Lines match when AlvoEspecifico is null or equals alvoEspecifico (ordinal, ignore case). RecursoAtual lines never match here.</summary>
    public int Ajustar(AlvoDeCondicao alvo, string? alvoEspecifico, int valorBase);
    /// <summary>The same result expressed as the delta to add to valorBase.</summary>
    public int Delta(AlvoDeCondicao alvo, string? alvoEspecifico, int valorBase);
    public string? DesabilitadoPor(CategoriaDesabilitavel categoria, Guid itemId); // condition name, or null
    public IReadOnlyList<ExplicacaoDeCondicao> Explicar(); // per condition: name and human-readable lines
    public IReadOnlyList<(CondicaoAtiva Condicao, ModificadorDeCondicao Linha)> AjustesDeRecursoAtual(MomentoDeCondicao momento);
}
public static class DuracaoDeCondicao
{
    public static int? TurnosRestantes(int aplicadaNoTurno, int? duracao, int turnoAtual);   // null = sem prazo
    public static bool Expirou(int aplicadaNoTurno, int? duracao, int turnoAtual);           // turnoAtual >= max(aplicada,1) + duracao
    public static int ValorDoAjuste(ModificadorDeCondicao linha, int totalDoRecurso);        // fixed, or floor(total × pct / 100)
}
```

`Explicar` lines read, e.g., "Força −2", "Todas as Perícias −10%", "Vitalidade total +5", "Vitalidade atual −3 a cada vez", "Desabilita: Armas" (labels for enums come from a small Portuguese table inside the Domain file).

- [ ] Unit tests: fixed only; percent only; both (percent over base + fixed); two conditions adding up; general vs. specific target; rounding down for negative results (−50% of 5 → −3, i.e. `Ajustar(5) == 2`; state and test the rounding rule: floor of the adjusted value); a percent of −100 gives 0; `RecursoAtual` never returned by `Ajustar`; disable by category and by item, first condition's name wins; `Nenhum` returns the base; every row of `DuracaoDeCondicao` (applied in preparation counts from Turno 1; exact boundary); `ValorDoAjuste` fixed and percent.
- [ ] Commit — `feat(domain): motor de efeitos das Condições`.

---

### Task 6: Numeric effects on the sheets

**Files:** `src/RuinaRPG.Api/Services/CondicoesAtivasDaFicha.cs` (new, scoped), the three `*SheetStats.cs`, the sheet/attribute/skill/mastery/affinity controllers where a value is computed outside the stats services, Contracts (sheet responses gain what is listed below), tests `tests/RuinaRPG.Tests.Integration/Condicoes/CondicoesNaFichaTests.cs`.

`CondicoesAtivasDaFicha`: `Task<EfeitosDeCondicao> ParaPersonagemAsync(Guid sheetId)`, `ParaNpcAsync(NpcSheet sheet)`, `ParaCriaturaAsync(CreatureSheet sheet)` — the active conditions of every participant linked to that sheet in `EmAndamento` encounters, not expired, all summed; **always `Nenhum` for an NPC/Criatura with `OwnerId == null`**; one query, cached per request per sheet. `Task<EfeitosDeCondicao> DoParticipanteAsync(Guid participantId)` for the summary (Task 11), which ignores the granted rule.

Where each target lands (read the three stats services and the call sites of `ModificadoresAsync` / `ArtifactBonusCalculator.Sum` first; they are the single modifier path):

| Alvo | Rule |
|---|---|
| Atributo | the attribute total after Artefatos/penalties goes through `Ajustar(Atributo, nome, total)`; every consumer of the attribute total (skills, sub-attributes, resource maximums, masteries) receives the adjusted total |
| Pericia | the skill total (already built on the adjusted attribute) goes through `Ajustar(Pericia, chave, total)` |
| SubAtributo | each sub-attribute result goes through `Ajustar(SubAtributo, constante, valor)` |
| Dano | each of the four damage-type modifiers goes through `Ajustar(Dano, tipo, valor)` |
| RecursoTotal | Vitalidade, Foco, Adrenalina and Estresse maximums go through `Ajustar(RecursoTotal, recurso, máximo)`, floor 0 |
| AfinidadeElemental | the value shown for each Afinidade row of that Elemento; the Vocação Arcana counter still uses the stored values |
| DanoElemental | a new response field per Elemento on the sub-attributes/dano response, 0 when no condition |
| Maestria | the Maestria total goes through `Ajustar(Maestria, chaveDaPericia, total)` |

Implement by threading one `EfeitosDeCondicao` through each stats service's public methods (default `Nenhum` keeps other callers unchanged), not by scattering lookups. Budgets, limits, `FichaParaRequisitos` (Passiva requirement checks) and equipment-requirement checks must keep using values **without** conditions: where a stats method serves both display and a check, give it the parameter and pass `Nenhum` from the check.

Stored values: nothing computed with a condition is written. The existing rule "Atual não pode exceder o máximo" on sheet saves must clamp against the maximum **with** conditions (so a player cannot save above the reduced total), and that clamp is a real, permanent change (spec). The generic sheet PUT of an open page must not otherwise change stored data because a condition is active (Review Focus 4): add a test that GETs a sheet under a condition, PUTs it back unchanged, removes the condition and finds every stored and computed value equal to the start.

New endpoint `GET api/{character|npc|creature}-sheets/{id}/condicoes-ativas` (who can edit the sheet): `[{ Nome, Descricao, Icone, TurnosRestantes?, Linhas: [string] }]` from `Explicar()`.

- [ ] Integration tests (Personagem unless stated): each row of the table with a fixed and with a percent line, asserting the exact number before, during and after (remove, expire, finish; reopen reapplies; Turno 0 does nothing) — Review Focus 1; an Atributo −50% changes a dependent Perícia and Sub-atributo exactly once — Review Focus 5; two encounters in progress sum; a granted NPC and a granted Criatura behave like Personagem; a GM-owned NPC's sheet endpoints are unchanged while a participant of it carries a condition — Review Focus 2; attribute/skill point budgets and a Passiva requirement are unaffected; the unchanged-PUT round trip above.
- [ ] Docs: `Requisitos - Ficha de Personagem.md` new requirement "Condições ativas" (what changes, when, that budgets and requirements ignore them), and the NPC/Criatura diffs (granted vs. GM-owned). Commit — `feat(api): Condições ativas alteram os valores calculados da ficha`.

---

### Task 7: Disables

**Files:** the stats services and arsenal/possessions/spell/rune controllers that list rows, Contracts (list responses gain `string? DesabilitadoPor`), tests in `CondicoesNaFichaTests.cs`.

- A disabled Arma, Escudo, Armadura or Artefato counts as **not in use** for every calculation that goes through the stats services (defenses, Artefato bonuses, equipment penalties, damage modifiers). Its stored `IsEquipped`/slot is untouched.
- A disabled Magia, Habilidade, Runa or Passiva is only marked.
- Every list response of those eight kinds on the three sheets carries `DesabilitadoPor` (the condition's name) for an affected row, null otherwise; whole-category and specific-item disables both work; Racial entries are never disabled.
- Requirement and limit checks ignore disables.

- [ ] Tests: an Artefato's bonus disappears and comes back; an Escudo stops counting in Defesa; a disabled Arma's equipment penalty disappears; specific-item disable leaves the other items of the category alone; `DesabilitadoPor` on each of the eight list endpoints; GM-owned NPC unaffected; stored equip state unchanged after the condition leaves.
- [ ] Commit — `feat(api): Condições desabilitam equipamentos, magias, runas e passivas`.

---

### Task 8: Activation changes — clamp, sheet log attribution and notifications

**Files:** `src/RuinaRPG.Infrastructure/SheetLogs/ICurrentActor.cs` (+ scope override), `SheetLogEntry.cs` (+ `Automatico` bool) + migration `AddAutomaticoToSheetLogEntries`, `SheetLogInterceptor.cs`, `SheetLogEntryResponse` (+ `Automatico`), `LogDaFichaDialog.razor` (an "automático" chip), `src/RuinaRPG.Api/Services/CondicoesDoEncontro.cs` (new, scoped), the encounter controllers, `NotificationHub` usage, tests.

`CondicoesDoEncontro.AoMudarAtivacaoAsync(IEnumerable<Guid> participantIds)` — called after apply, remove, start, finish, reopen, participant removal and expiry, **before** the action's save:
1. for each sheet-linked participant (Personagem or granted), recompute the four resource maximums with the conditions that will be active after this action and, if a stored Atual exceeds its new maximum, set it to the maximum (permanent);
2. write one `SheetLogEntry` per sheet "Condição {nome} aplicada/removida/expirada" (`Secao = "Condições"`, `Automatico = false` for apply/remove by the GM, `true` for expiry);
3. after the save, push `CondicoesAlteradas` (payload: sheet kind and id) through `IHubContext<NotificationHub>` to the sheet's `OwnerId`, and `ParticipantsChanged` to the encounter group. Hub failures are logged and never fail the request (same pattern as `NotifyAffectedEncountersAsync`).

Actor for server-made changes (Plan B final review I3): `ICurrentActor` gains a way to mark the current scope's writes as automatic (e.g. `IDisposable Automatico()` setting a scoped flag); the interceptor stores it in `SheetLogEntry.Automatico`; the popup shows a chip "automático" next to the actor. The clamp of step 1 and every Task 9 adjustment are written inside such a scope.

`EncounterLogDaFicha` must not add a `RecursoDaFicha` row for a change made inside an automatic scope (the condition code writes its own encounter-log row).

- [ ] Tests: a −10 Vitalidade total with Atual above the new total clamps on apply and does not restore on removal; the same on "Iniciar" for a condition applied in preparation; the sheet log shows the application by the GM and the clamp as automatic, and the endpoint returns `Automatico`; the owner receives `CondicoesAlteradas` (use the hub-test harness under `tests/RuinaRPG.Tests.Integration/Hubs`) on apply, remove, start, finish, reopen; no `RecursoDaFicha` row for the clamp; a hub failure does not fail the request.
- [ ] Docs: Técnico R0012 (the automatic flag), Modelo de Dados. Commit — `feat(api): ativação de Condições ajusta recursos, registra no log e avisa o dono da ficha`.

---

### Task 9: Per-turn adjustments and expiry

**Files:** `CondicoesDoEncontro.cs`, `EncounterTurnos.cs` call sites, tests `tests/RuinaRPG.Tests.Integration/Condicoes/CondicoesNoTurnoTests.cs`.

`CondicoesDoEncontro.AoMudarAVezAsync(Encounter, MudancaDeVez)` — called by the four actions right after `DarAVezAsync`, before the save:

1. **Expiry**, when `EntrouEmNovoTurno`: every applied condition of the encounter with `DuracaoDeCondicao.Expirou(...)` is removed; log `CondicaoExpirada` (Texto = name, ParticipanteNome); then `AoMudarAtivacaoAsync` for the affected participants.
2. **`AoAplicar` lines**: run once per applied condition, the first time the condition is active — at apply time when the encounter is in progress, or at `Inicio` for conditions applied in preparation. Tracked in `AjustesAplicados` with `Turno = 0`.
3. **`ACadaVez` lines**, when `ParticipanteRecebeuAVez`: for each active condition of that participant, each line not already in `AjustesAplicados` for `(LinhaId, RoundNovo)` — Review Focus 3.
4. Where an adjustment lands: sheet-linked participant → the sheet's Atual, clamped to 0..max-with-conditions, inside an automatic scope; GM-owned instance → `PVAtual`/`PFAtual`/`PAAtual`, floor 0; Estresse on an instance → skipped. Value from `DuracaoDeCondicao.ValorDoAjuste` with the resource's total (for an instance, the source sheet's computed maximum **with that participant's conditions**; if the source sheet is gone, percent lines are skipped).
5. Each adjustment writes an `AjusteDeCondicao` encounter-log row (ParticipanteNome, Campo = resource label, before → after, Texto = condition name); a skipped one writes a row whose Texto says why ("Estresse não é acompanhado no encontro" / "ficha de origem removida").

`AoAplicar` at apply time (encounter already in progress) is triggered from the apply endpoint of Task 4.

- [ ] Tests: `AoAplicar` at apply and at start, once; `ACadaVez` on "Próxima vez" and "Passar a vez", not on "Vez anterior"; previous-then-next does not repeat in the same Turno and does apply again in the next Turno; removal hand-over applies to the receiver; clamp at 0 and at max; percent of total; instance vs. sheet target; Estresse skipped on an instance with the log line; expiry exactly at the boundary Turno, for a condition applied in preparation and one applied mid-fight; an expired condition is not brought back by "Vez anterior"; expiry restores totals (ties to Task 6) and notifies the owner; the encounter log rows and their order; two racing advances → one applies (Task 1's 409).
- [ ] Docs: `Requisitos - Gerenciador de Encontros.md` — rewrite R0003's "Condições" bullet and add the requirement(s) for applying catalog conditions, duration, per-turn adjustments, expiry and what goes into the log. Commit — `feat(api): ajustes por vez e expiração de Condições no encontro`.

---

### Task 10: Client — conditions on the encounter page and on the sheets

**Files:** `GerenciadorDeEncontros.razor`, `Shared/AplicarCondicaoDialog.razor`, `Shared/CondicoesAtivasAlert.razor`, `Shared/DesabilitadoChip.razor`, the three sheet pages, `LogDoEncontroAgrupador.cs` (texts for the new tipos), `Services` (subscribe to `CondicoesAlteradas` through the existing notification connection), tests.

- Encounter page: the "Condições" column shows chips — catalog ones with icon, name, remaining Turnos ("3 turnos", nothing when no duration), a muted style when not active, and a remove (x); legacy texts as plain chips with remove only. The free-text field leaves the edit form. Button "Adicionar condição" per row (not when finished) opens `AplicarCondicaoDialog`: searchable select of the catalog (`MudAutocomplete`), description shown on selection, Duração (prefilled from the catalog, clearable), the lines with editable values, and per disabled category a choice "toda a categoria" or a multi-select of that participant's rows from `itens-desabilitaveis`.
- Log texts: `CondicaoExpirada` → "{nome}: a condição {Texto} expirou"; `AjusteDeCondicao` → "{nome} — {Campo}: {antes} → {depois} ({Texto})", or "{nome} — {Texto}" when there are no values.
- Sheets: `CondicoesAtivasAlert` at the top of `FichaDePersonagem`, and of `FichaDeNpc`/`FichaDeCriatura` when the sheet is granted — a `MudAlert` listing each active condition with icon, name, description, its lines and remaining Turnos; nothing rendered when none. Rows of the eight kinds show `DesabilitadoChip` ("Desabilitado por: {Condição}") when `DesabilitadoPor` is set. On `CondicoesAlteradas` for this sheet the page reloads the sheet data and the alert **without** triggering a save, and without discarding a field the user is editing if that can be avoided (at minimum: do not overwrite the focused field).
- Totals changed by a condition need no extra UI: the sheet already shows computed totals; the alert explains why.

- [ ] bUnit tests: chips (catalog, inactive, legacy); the dialog's request body with adjusted values, duration and specific items; duplicate error shown; the alert renders and is absent when empty; disabled chips on a weapon row and a spell row; the page reloads on the notification for its own sheet and ignores another sheet's; the two new log texts.
- [ ] Commit — `feat(client): Condições no encontro e nas fichas`.

---

### Task 11: Summary of an NPC/Criatura participant

**Files:** Contracts `ResumoDeFichaResponse.cs`, `src/RuinaRPG.Api/Controllers/EncounterResumoController.cs` (or an action on the participants controller), `Shared/ResumoDaFichaDialog.razor`, `GerenciadorDeEncontros.razor`, tests.

`GET api/encounters/{id}/participants/{pid}/resumo` — GM of the campaign; NPC or Criatura participant (granted or not); 400 for a Personagem participant; 404 when the source sheet is gone. Built from the same stats services with `CondicoesAtivasDaFicha.DoParticipanteAsync(pid)` — so for a GM-owned sheet the conditions of **that instance** are applied here and only here (Review Focus 2); for a granted sheet the numbers equal the sheet's own.

Response: name, image URL, kind, Nível or Rank; attributes; sub-attributes and defenses; resources (current/total — the instance's PV/PF/PA for a GM-owned participant); skills with points; equipment in use; Magias & Habilidades; Características; active conditions (from `Explicar()`). Every numeric value is a pair `(Valor, ValorSemCondicoes)` so the client can highlight; every equipment/spell row carries `DesabilitadoPor`.

Dialog: icon button (`Icons.Material.Filled.Visibility`, `aria-label="Resumo da ficha"`) on each NPC/Criatura row; read-only; a changed value is highlighted with the original struck through next to it; disabled rows show the chip; a link opens the full sheet in a new tab.

- [ ] Tests: API — GM-owned NPC with a condition: summary shows modified numbers while the sheet's own endpoints do not; two instances of one sheet show different numbers; granted sheet equals its sheet; Personagem → 400; another GM → 404; deleted source → 404. Client — highlight only where the pair differs; the button only on NPC/Criatura rows.
- [ ] Docs: Gerenciador de Encontros (new requirement for the summary). Commit — `feat: resumo da ficha de NPC e Criatura no encontro, com as Condições aplicadas`.

---

### Task 12: Log-map coverage for the new tables

**Files:** `tests/RuinaRPG.Tests.Unit/SheetLogs/SheetLogMapTests.cs`, `SheetLogMap.cs` if needed.

Plan B's final review (M2–M4): the completeness test finds sheet tables by namespace and name prefix. Replace the discovery by the EF model: every entity type with a foreign key to `CharacterSheet`, `NpcSheet` or `CreatureSheet` must be in `SheetLogMap.Entidades` or in an explicit exclusion list with a reason (`SheetLogEntry`, `EncounterParticipant`, `CampaignAttachment`, the História image links...). Build the model without a database (`new DbContextOptionsBuilder().UseNpgsql("Host=unused")`; no connection is opened to read `Model`). Check the previously ignored columns (`EAPAtual`, `Circulo`, `Grau`, `PontosDeIgnicaoTotal`) are still unwritten after this plan; label any that is now written.

- [ ] Commit — `test(infra): completude do log de ficha descoberta pelo modelo do EF`.

---

### Task 13: Release 1.5.0

**Files:** `src/RuinaRPG.Domain/AppVersionInfo.cs`, `src/RuinaRPG.Client/Shared/ChangelogDialog.razor` (+ its test), `Docs/` as below.

- `AppVersionInfo.Current = "1.5.0"`.
- `ChangelogDialog`: the 1.5.0 entry, in the dialog's existing style, covering the nine items — características de criatura só do catálogo de criatura; Pontos de Ignição acompanham o nível e viram saldo das Magias/Habilidades (with the note that fichas antigas com PI 0 precisam ter o PI lançado para criar magias com custo); edição de Magias/Habilidades montadas na ficha; log de ficha para o GM; encontros com fases, controle de vez, finalizados e log (instâncias repetidas numeradas); Condições; resumo da ficha no encontro. Auditor-only section: o atalho de característica de criatura na ficha e a página Auditoria → Condições.
- Deploy notes: this release has schema migrations that the running code depends on immediately (the sheet log writes on every character-sheet save). State in `Requisitos - Técnico.md`, in the deploy requirement, that for 1.5.0 `make migrate` must run right after `make deploy`, with players off the app, and why. Do not change `Makefile` or deploy scripts.
- Final verification: `dotnet build --no-incremental` (0 warnings), Unit and Client projects in full; the integration project is run in slices by the controller after this task.

- [ ] Commit — `chore(release): versão 1.5.0`.
