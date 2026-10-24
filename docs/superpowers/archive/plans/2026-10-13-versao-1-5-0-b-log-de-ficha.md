# Versão 1.5.0 — Plano B: Log de Ficha

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Record every change to a Ficha de Personagem, and to an NPC or Criatura sheet granted to a player, with who, when and the value before and after, and let the GM read that log in a popup opened from the campaign page.

**Architecture:** A single EF Core `SaveChangesInterceptor` walks the `ChangeTracker` in `SavingChanges` and adds `SheetLogEntry` rows to the same save, so no sheet controller changes. A static map says, per entity type, which sheet it belongs to, its screen section and the Portuguese label of each property; a reflection test fails when a sheet table or property is neither mapped nor explicitly ignored. One GM-only endpoint pages the log; one dialog shows it.

**Tech Stack:** .NET 8, EF Core 8 interceptors + Npgsql, ASP.NET Core, Blazor WebAssembly + MudBlazor, xUnit + FluentAssertions, Testcontainers, bUnit.

**Spec:** `docs/superpowers/archive/specs/2026-10-13-versao-1-5-0-b-log-de-ficha-design.md` — read it before any task. Where this plan and the spec disagree, the spec wins.

## Global Constraints

- Branch `release/1.5.0`. Commit after every task and `git push`.
- `dotnet build` ends with **0 warnings, 0 errors**.
- **TDD is mandatory**: failing test first, RED and GREEN evidence in the report.
- Integration tests need Docker. Known ~4% timeout flake: a test that fails with a timeout and passes when rerun **alone** is the flake.
- Disk is tight (~1.8 GB free): run filtered tests, one `dotnet test` at a time, never the whole integration project in one invocation.
- UI text and `Docs/` are Brazilian Portuguese. MudBlazor icons, never emojis. `Docs/` uses Obsidian `[[wikilinks]]`; requirement IDs are never renumbered.
- Do not bump `AppVersionInfo` or touch `ChangelogDialog` (end of Plan D).
- Never stage the untracked `Docs/harp_notification.mp3`. Stage by path.
- End every commit message with the attribution trailer your own session's system reminder specifies.
- Who sees the log: **only the GM**. The sheet's owner gets 404, like a stranger.
- Which sheets are logged: every Ficha de Personagem; an NPC or Criatura sheet **only while `OwnerId` is set** (granted). A save with no authenticated user (seeders, startup, tests writing straight to the database) logs nothing.
- The log and the change it describes commit together or not at all.
- Long texts (`Historia`, every `Descricao`, `Requisitos` jsonb) are logged as changed, without their content.
- The character diary and the História image links are never logged.

## Review Focus

1. **A new sheet table or column added later is silently unlogged.** The map-completeness test must fail for an unmapped sheet entity and for an unlabelled, un-ignored property. Pinned in Task 3.
2. **An autosave that changes nothing must write no log line.** The sheet pages resend the whole form; a property whose value is equal before and after is not a change. Pinned in Task 4.
3. **A failed request leaves no log.** If the save throws (constraint violation), no `SheetLogEntry` survives. Pinned in Task 4.
4. **A sheet with thousands of lines.** The endpoint pages by keyset and never loads the whole log; the dialog loads more on demand. Pinned in Tasks 5 and 6.
5. **Day grouping at midnight in the browser's time zone.** An entry at 23:30 local and one at 00:10 local the next day land in different groups, whatever the UTC date. Pinned in Task 6.

---

## File map

| File | Responsibility | Task |
|---|---|---|
| `src/RuinaRPG.Client/Services/AutoSaveCoordinator.cs` | one pending save per channel | 1 |
| `src/RuinaRPG.Infrastructure/SheetLogs/SheetLogEntry.cs`, `SheetLogEnums.cs` | entity and enums | 2 |
| `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs` + migration | table, FKs, indexes | 2 |
| `src/RuinaRPG.Infrastructure/SheetLogs/SheetLogMap.cs` | entity → sheet, section, labels, ignores | 3 |
| `src/RuinaRPG.Infrastructure/SheetLogs/SheetLogValues.cs` | value rendering | 3 |
| `src/RuinaRPG.Infrastructure/SheetLogs/ICurrentActor.cs`, `SheetLogInterceptor.cs` | recording | 4 |
| `src/RuinaRPG.Api/Services/HttpCurrentActor.cs`, `Program.cs` | actor from the request, registration | 4 |
| `src/RuinaRPG.Contracts/SheetLogs/*.cs`, `src/RuinaRPG.Api/Controllers/SheetLogsController.cs` | read endpoint | 5 |
| `src/RuinaRPG.Client/Shared/LogDaFichaDialog.razor`, `Pages/CampanhaDetalhe.razor` | popup and icons | 6 |

---

### Task 1: `AutoSaveCoordinator` keeps one pending save per channel

Carried over from Plan A's final review: since the PI field got its own save, a PI edit and another field commit within 400 ms cancel one another, because the coordinator keeps a single pending delegate. The NPC/Criatura Nível and Experiência fields have the same problem.

**Files:**
- Modify: `src/RuinaRPG.Client/Services/AutoSaveCoordinator.cs`
- Modify: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor`, `FichaDeNpc.razor`, `FichaDeCriatura.razor` (the `_autoSave.NotifyChanged(...)` calls for PI, Nível and Experiência)
- Test: `tests/RuinaRPG.Tests.Client/Services/AutoSaveCoordinatorTests.cs`

**Interfaces:**
- Produces: `void NotifyChanged(Func<Task<bool>> validateAndSaveAsync, string canal = "geral")`. A call cancels only the pending save of the **same** `canal`. Saves of all channels still run one at a time, in the order their debounce expired.

- [ ] **Step 1: Write the failing tests** in `AutoSaveCoordinatorTests` (follow the file's existing way of waiting out the 400 ms debounce):

```csharp
[Fact]
public async Task Saves_on_different_channels_do_not_cancel_each_other()
{
    var coordinator = new AutoSaveCoordinator();
    var ran = new List<string>();

    coordinator.NotifyChanged(() => { ran.Add("geral"); return Task.FromResult(true); });
    coordinator.NotifyChanged(() => { ran.Add("pi"); return Task.FromResult(true); }, "pi");
    await Task.Delay(700);

    ran.Should().BeEquivalentTo(new[] { "geral", "pi" });
}

[Fact]
public async Task A_second_call_on_the_same_channel_still_replaces_the_first()
{
    var coordinator = new AutoSaveCoordinator();
    var ran = new List<string>();

    coordinator.NotifyChanged(() => { ran.Add("pi-1"); return Task.FromResult(true); }, "pi");
    coordinator.NotifyChanged(() => { ran.Add("pi-2"); return Task.FromResult(true); }, "pi");
    await Task.Delay(700);

    ran.Should().Equal("pi-2");
}

[Fact]
public async Task Saves_of_different_channels_never_overlap()
{
    var coordinator = new AutoSaveCoordinator();
    var running = 0; var maxRunning = 0;
    async Task<bool> Save() { maxRunning = Math.Max(maxRunning, ++running); await Task.Delay(100); running--; return true; }

    coordinator.NotifyChanged(Save);
    coordinator.NotifyChanged(Save, "pi");
    await Task.Delay(900);

    maxRunning.Should().Be(1);
}
```

- [ ] **Step 2: Run and see the first test fail** — `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~AutoSaveCoordinatorTests`. Expected: `ran` is only `["pi"]`.

- [ ] **Step 3: Implement.** Replace the single `_debounceCts` with a per-channel dictionary; everything after the debounce is unchanged:

```csharp
    private readonly Dictionary<string, CancellationTokenSource> _debounceCts = new();

    /// <summary>
    /// Call from every in-scope field's commit (blur, or selection change for non-text controls).
    /// Resets the debounce window of <paramref name="canal"/>; the delegate only actually runs once no
    /// further call arrives on that channel within DebounceMilliseconds. Fields that save through
    /// their own endpoint (Pontos de Ignição, Nível, Experiência) use their own channel, so a commit
    /// of another field right after them does not cancel their save. Saves of every channel still run
    /// one at a time.
    /// </summary>
    public void NotifyChanged(Func<Task<bool>> validateAndSaveAsync, string canal = "geral")
    {
        if (_debounceCts.TryGetValue(canal, out var pendente))
            pendente.Cancel();
        var cts = new CancellationTokenSource();
        _debounceCts[canal] = cts;
        _ = DebounceAndSaveAsync(validateAndSaveAsync, cts.Token);
    }
```

- [ ] **Step 4: Give the dedicated saves their own channel.** In the three pages, find every `_autoSave.NotifyChanged(X)` whose `X` is not the page's generic `SaveIfValidAsync`: `SavePontosDeIgnicaoAsync` → `"pi"`, `SaveNivelAsync` → `"nivel"`, `SaveExperienciaAtualAsync` → `"xp"`. Leave the generic calls without a channel. List in the report every call you changed, with file and line.

- [ ] **Step 5: Run** `dotnet test tests/RuinaRPG.Tests.Client` in full → PASS. `dotnet build` → 0 warnings.

- [ ] **Step 6: Commit** — `fix(client): autosave mantém um salvamento pendente por canal (PI, Nível e XP não se cancelam)`.

---

### Task 2: `SheetLogEntries` table

**Files:**
- Create: `src/RuinaRPG.Infrastructure/SheetLogs/SheetLogEntry.cs`, `src/RuinaRPG.Infrastructure/SheetLogs/SheetLogEnums.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Create: migration `AddSheetLogEntries`
- Modify: `Docs/Requisitos/Requisitos - Modelo de Dados.md`
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/SheetLogEntryMigrationTests.cs`

**Interfaces — produces:**

```csharp
namespace RuinaRPG.Infrastructure.SheetLogs;

public enum SheetLogCategoria { Geral, RecursoVFA, Estresse }
public enum SheetLogAcao { Alterou, Adicionou, Removeu, Criou }

/// <summary>Uma linha do log de ficha (Campanha R0016). Exatamente uma das três FKs de ficha é preenchida.</summary>
public class SheetLogEntry
{
    public Guid Id { get; set; }
    public Guid? CharacterSheetId { get; set; }
    public Guid? NpcSheetId { get; set; }
    public Guid? CreatureSheetId { get; set; }
    public DateTime OccurredAt { get; set; }
    public Guid ActorUserId { get; set; }
    public required string ActorNickname { get; set; }
    public bool ActorIsGm { get; set; }
    public SheetLogCategoria Categoria { get; set; }
    public SheetLogAcao Acao { get; set; }
    public required string Secao { get; set; }
    public string? Campo { get; set; }
    public string? ItemNome { get; set; }
    public string? ValorAntes { get; set; }
    public string? ValorDepois { get; set; }
}
```

`DbSet<SheetLogEntry> SheetLogEntries`.

- [ ] **Step 1: Failing migration test**, modelled on `Persistence/CreatureExclusiveTraitMigrationTests.cs` (same fixture and `MigrateAsync` pattern): after migrating, (a) the applied migrations contain one ending in `AddSheetLogEntries`; (b) inserting a `CharacterSheet` (with the minimum related rows that file's neighbours use to create one) and a `SheetLogEntry` pointing to it, then deleting the sheet, leaves **zero** `SheetLogEntries` (cascade); (c) the same for an `NpcSheet` and a `CreatureSheet`; (d) `pg_indexes` has an index on `SheetLogEntries` whose definition contains `"CharacterSheetId"` and `"OccurredAt"`.

- [ ] **Step 2: Run, see the compile failure.**

- [ ] **Step 3: Entity, enums, configuration.** In `OnModelCreating`, next to the configuration of other sheet-linked tables, following the file's style:

```csharp
        builder.Entity<SheetLogEntry>(e =>
        {
            e.HasOne<CharacterSheet>().WithMany().HasForeignKey(l => l.CharacterSheetId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<NpcSheet>().WithMany().HasForeignKey(l => l.NpcSheetId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<CreatureSheet>().WithMany().HasForeignKey(l => l.CreatureSheetId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(l => new { l.CharacterSheetId, l.OccurredAt }).IsDescending(false, true);
            e.HasIndex(l => new { l.NpcSheetId, l.OccurredAt }).IsDescending(false, true);
            e.HasIndex(l => new { l.CreatureSheetId, l.OccurredAt }).IsDescending(false, true);
        });
```

Store the two enums the way the context stores its other enums (check a neighbour such as `Polaridade`; if they use `HasConversion<string>()`, do the same).

- [ ] **Step 4: Migration** — `dotnet ef migrations add AddSheetLogEntries --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations`. The migration must only create `SheetLogEntries` with its three FKs and three indexes. Unrelated changes mean the snapshot was out of sync: stop and report.

- [ ] **Step 5: Run the migration test → PASS.**

- [ ] **Step 6: Docs.** `Requisitos - Modelo de Dados.md`: new table `SheetLogEntries` with every column above, the "exactly one FK" rule, the cascade, and one line saying rows are written only by the save interceptor (ver "[[Requisitos - Técnico]]").

- [ ] **Step 7: Commit** — `feat(infra): tabela SheetLogEntries para o log de ficha`.

---

### Task 3: The map and the value renderer

**Files:**
- Create: `src/RuinaRPG.Infrastructure/SheetLogs/SheetLogMap.cs`, `src/RuinaRPG.Infrastructure/SheetLogs/SheetLogValues.cs`
- Test: `tests/RuinaRPG.Tests.Unit/SheetLogs/SheetLogMapTests.cs`, `tests/RuinaRPG.Tests.Unit/SheetLogs/SheetLogValuesTests.cs` (the Unit project references Infrastructure)

**Interfaces — produces:**

```csharp
public enum SheetLogKind { Character, Npc, Creature }

/// <summary>How one entity type takes part in the sheet log.</summary>
public sealed record SheetLogEntityMap(
    SheetLogKind Kind,
    bool IsSheet,                       // the sheet row itself
    string Secao,                       // screen section, e.g. "Atributos"
    string? SheetIdProperty,            // FK to the sheet; null when IsSheet or when ParentType is set
    Type? ParentType,                   // for a child of a child (spell effect → spell ability)
    string? ParentIdProperty,
    IReadOnlyDictionary<string, string> Campos,   // property → Portuguese label
    IReadOnlySet<string> Ignorados,               // properties never logged
    IReadOnlySet<string> TextosLongos,            // logged as "alterado", no values
    IReadOnlyDictionary<string, SheetLogCategoria> Categorias, // property → categoria (default Geral)
    string? ItemNomeProperty,           // property whose value names the row (Nome, Atributo, Slot…)
    string? ItemRefProperty);           // or a FK whose target names the row: "ItemId", "PericiaId", "TraitId", "ArtifactItemId", "CreatureExclusiveTraitId"

public static class SheetLogMap
{
    public static IReadOnlyDictionary<Type, SheetLogEntityMap> Entidades { get; }
    /// <summary>Entity types that look like sheet tables but are deliberately not logged.</summary>
    public static IReadOnlySet<Type> ForaDoLog { get; }
}

public static class SheetLogValues
{
    /// <summary>bool → "Sim"/"Não"; enum → its screen label; null → "—"; decimal without trailing zeros; other → ToString().</summary>
    public static string Render(object? valor);
}
```

- [ ] **Step 1: Write the failing tests.**

`SheetLogValuesTests`: `true` → "Sim", `false` → "Não", `null` → "—", `12` → "12", `1.50m` → "1,5" (pt-BR), an enum with an existing display helper renders its label (find how the client/API already turn `Vocacao`/`Cobertura`/`Atributo` values into labels; if the label helpers live in the Client, render the enum name with no transformation and say so in the report).

`SheetLogMapTests` — the completeness tests that are this task's point (Review Focus 1):

```csharp
    private static readonly Assembly Infra = typeof(RuinaRpgDbContext).Assembly;

    private static IEnumerable<Type> EntidadesDeFicha() =>
        typeof(RuinaRpgDbContext).GetProperties()
            .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(p => p.PropertyType.GetGenericArguments()[0])
            .Where(t => t.Namespace is "RuinaRPG.Infrastructure.CharacterSheets" or "RuinaRPG.Infrastructure.NpcSheets" or "RuinaRPG.Infrastructure.CreatureSheets")
            .Where(t => t.Name.StartsWith("Character") || t.Name.StartsWith("Npc") || t.Name.StartsWith("Creature"));

    [Fact]
    public void Every_sheet_entity_is_either_mapped_or_explicitly_left_out()
    {
        var semDestino = EntidadesDeFicha().Where(t => !SheetLogMap.Entidades.ContainsKey(t) && !SheetLogMap.ForaDoLog.Contains(t));
        semDestino.Select(t => t.Name).Should().BeEmpty("toda tabela de ficha precisa estar no SheetLogMap ou em ForaDoLog");
    }

    [Fact]
    public void Every_scalar_property_of_a_mapped_entity_is_labelled_ignored_or_long_text()
    {
        var faltando = new List<string>();
        foreach (var (tipo, mapa) in SheetLogMap.Entidades)
            foreach (var p in tipo.GetProperties().Where(p => p.PropertyType == typeof(string) || p.PropertyType.IsValueType
                         || Nullable.GetUnderlyingType(p.PropertyType) is not null || p.Name == "Requisitos"))
                if (!mapa.Campos.ContainsKey(p.Name) && !mapa.Ignorados.Contains(p.Name) && !mapa.TextosLongos.Contains(p.Name))
                    faltando.Add($"{tipo.Name}.{p.Name}");
        faltando.Should().BeEmpty();
    }

    [Fact]
    public void Only_the_diary_free_history_images_are_left_out()
    {
        SheetLogMap.ForaDoLog.Select(t => t.Name).Should().BeEquivalentTo(
            new[] { "CharacterSheetHistoriaImage", "NpcSheetHistoriaImage" });
    }

    [Fact]
    public void The_resource_fields_carry_their_categoria_on_the_three_sheets()
    {
        foreach (var tipo in new[] { typeof(CharacterSheet), typeof(NpcSheet), typeof(CreatureSheet) })
        {
            var c = SheetLogMap.Entidades[tipo].Categorias;
            c["VitalidadeAtual"].Should().Be(SheetLogCategoria.RecursoVFA);
            c["FocoAtual"].Should().Be(SheetLogCategoria.RecursoVFA);
            c["AdrenalinaAtual"].Should().Be(SheetLogCategoria.RecursoVFA);
        }
        SheetLogMap.Entidades[typeof(CharacterSheet)].Categorias["EstresseAtual"].Should().Be(SheetLogCategoria.Estresse);
        SheetLogMap.Entidades[typeof(NpcSheet)].Categorias["EstresseAtual"].Should().Be(SheetLogCategoria.Estresse);
    }

    [Fact]
    public void A_spell_effect_points_to_its_spell_ability_as_parent()
    {
        var m = SheetLogMap.Entidades[typeof(CharacterSpellAbilityEffect)];
        m.ParentType.Should().Be(typeof(CharacterSpellAbility));
        m.ParentIdProperty.Should().Be("CharacterSpellAbilityId");
    }
```

- [ ] **Step 2: Run, see them fail (types missing).**

- [ ] **Step 3: Implement `SheetLogValues`** as specified.

- [ ] **Step 4: Implement `SheetLogMap`.** One entry per entity. Rules for filling it:

  - **Labels (`Campos`) are the labels the sheet pages show.** Open `FichaDePersonagem.razor`, `FichaDeNpc.razor` and `FichaDeCriatura.razor`, find the field bound to each property and copy its `Label` (e.g. `VitalidadeAtual` → "Vitalidade Atual", `NucleosRankF` → "Âmbares Absorvidos — Rank F", `PossuiCoracaoDeMana` → "Possui Coração de Mana?"). A property with no field on screen that still changes because of a user action (e.g. `Nivel` on Personagem, `PontosDeIgnicaoBonusManual`) gets a plain Portuguese label. NPC and Personagem share most labels: build the shared dictionary once and reuse it.
  - **`Ignorados`:** `Id`; the FK to the sheet or to the parent; `CampaignId`, `OwnerId`, `GmId`; `LastDismissedLevelUpLevel`; columns that are stored but no longer used (`EAPAtual`, `Circulo`, `Grau` on the sheet rows if the controllers never write them — verify with grep; and `PontosDeIgnicaoTotal` on `CharacterSheet`); `SourceBankEntryId`; `CriadaNaFicha`; `IsRacial`, `RacialVariante`; navigation collections.
  - **`TextosLongos`:** `Historia`, every `Descricao`, `Requisitos`.
  - **Reference-valued properties stay in `Campos`** with a label (`HistoricoId` → "Histórico", `ImageId` → "Imagem", `EquipmentKitId` → "Equipagem", `ItemId` on an armor slot → "Item"): Task 4 renders their values by name.
  - **`Secao`**, by entity: the sheet row → "Informações Básicas"; `*Attribute` → "Atributos"; `*Skill` → "Perícias"; `*Affinity` → "Afinidades"; `*Mastery` → "Maestrias"; `*Weapon`, `*Shield`, `*ArmorSlot` → "Arsenal"; `*InventoryItem` → "Inventário"; `*Artifact` → "Artefatos"; `CreatureSpoil` → "Espólios"; `*SpellAbility` and `*SpellAbilityEffect` → "Magias & Habilidades"; `*Rune` → "Runas"; `*Affection` → "Afetos"; `*Trait` → "Características". Where a page uses a different section title for the same thing, the page's title wins.
  - **Row name:** `ItemNomeProperty` = `Nome` for affection/mastery/rune/spell ability; `Atributo` for attributes; `Slot` for armor slots; `Elemento` for affinities. `ItemRefProperty` = `ItemId` for weapon/shield/inventory/spoil, `ArtifactItemId` for artifacts, `PericiaId` for skills, `TraitId` for traits (and `CreatureExclusiveTraitId` is resolved by Task 4 when `TraitId` is null).
  - **`ForaDoLog`:** `CharacterSheetHistoriaImage`, `NpcSheetHistoriaImage`.

- [ ] **Step 5: Run** `dotnet test tests/RuinaRPG.Tests.Unit --filter FullyQualifiedName~SheetLogs` → PASS. `dotnet build` → 0 warnings.

- [ ] **Step 6: Commit** — `feat(infra): mapa do log de ficha com teste de completude`.

---

### Task 4: The interceptor

**Files:**
- Create: `src/RuinaRPG.Infrastructure/SheetLogs/ICurrentActor.cs`, `src/RuinaRPG.Infrastructure/SheetLogs/SheetLogInterceptor.cs`
- Create: `src/RuinaRPG.Api/Services/HttpCurrentActor.cs`
- Modify: `src/RuinaRPG.Api/Program.cs` (`AddDbContext`, registrations)
- Modify: `Docs/Requisitos/Requisitos - Técnico.md`
- Test: `tests/RuinaRPG.Tests.Integration/SheetLogs/SheetLogRecordingTests.cs`

**Interfaces:**
- Consumes: `SheetLogMap`, `SheetLogValues` (Task 3); `SheetLogEntry` (Task 2).
- Produces: `public interface ICurrentActor { Guid? UserId { get; } }`; `SheetLogInterceptor : SaveChangesInterceptor`, registered scoped and attached to `RuinaRpgDbContext`.

- [ ] **Step 1: Write the failing integration tests.** New class with the usual fixture/`ApiFactory` setup (copy the helpers for registering a GM and a linked player, creating a campaign, a member and a character sheet from `CharacterSheetsControllerTests`, and the grant helpers from `NpcSheetsControllerTests`). A helper reads the log straight from the database:

```csharp
    private async Task<List<SheetLogEntry>> LogAsync(Func<SheetLogEntry, bool> daFicha)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        return (await db.SheetLogEntries.AsNoTracking().OrderBy(l => l.OccurredAt).ToListAsync()).Where(daFicha).ToList();
    }
```

Tests (each asserts on real rows):

1. `Creating_a_sheet_logs_a_single_Criou_line` — after `POST …/character-sheets`: exactly one entry, `Acao == Criou`, actor is the GM, `ActorIsGm`.
2. `A_player_edit_logs_the_field_with_before_and_after` — the player PUTs the sheet changing `VitalidadeAtual` from the stored value to another valid one: one new entry with `Campo == "Vitalidade Atual"`, `ValorAntes`/`ValorDepois` as strings, `Categoria == RecursoVFA`, `ActorNickname` the player's, `ActorIsGm == false`.
3. `A_gm_edit_is_flagged_ActorIsGm`.
4. `Saving_the_same_values_again_logs_nothing` (Review Focus 2) — GET the sheet, PUT it back unchanged twice: the count does not grow.
5. `Estresse_Atual_gets_its_own_categoria`.
6. `A_long_text_is_logged_without_its_content` — `PUT …/historia`: one entry with `Campo == "História"` and both values null.
7. `Adding_and_removing_a_list_item_logs_Adicionou_and_Removeu_with_its_name` — add an affection named "Rival", then delete it.
8. `A_spell_built_on_the_sheet_logs_one_line_and_its_effects_are_folded_in` — give the sheet a PI balance through `PUT …/pontos-de-ignicao`, add a from-scratch Magia with two effects: exactly one `Adicionou` entry in "Magias & Habilidades" with `ItemNome` the spell name (plus the separate `Alterou` line for "Pontos de Ignição Atual"); no entry per effect.
9. `Editing_a_spell_logs_its_changed_fields_and_one_line_for_the_effects` — PUT the edit changing the name and the effects: an `Alterou` for "Nome" with before/after, and exactly one `Alterou` with `Campo == "Efeitos"` and null values.
10. `An_attribute_row_change_names_the_attribute` — change Força's Gasto: `Secao == "Atributos"`, `ItemNome` the attribute's label, `Campo` the Gasto label.
11. `A_reference_value_is_rendered_by_name` — set the sheet's Histórico: `ValorDepois` is the Histórico's name, not a Guid.
12. `A_non_granted_npc_is_not_logged_and_a_granted_one_is` — edit a GM NPC: zero entries; edit a blank NPC granted to a player (as the player): entries with `NpcSheetId` set.
13. `The_character_diary_is_not_logged` — add a character diary entry: zero new entries.
14. `A_failed_save_leaves_no_log` (Review Focus 3) — in a scope, with an `ICurrentActor` stub registered for the test (see Step 5) change a tracked sheet property **and** add a row that violates a constraint (e.g. a `CharacterAttribute` with a non-existent `CharacterSheetId`), call `SaveChangesAsync`, expect `DbUpdateException`; then assert the log has no new entry for the sheet.
15. `A_write_without_an_authenticated_user_logs_nothing` — change a sheet through a plain scope (no HTTP request): zero entries.

- [ ] **Step 2: Run, see them fail** (no entries are written).

- [ ] **Step 3: `ICurrentActor` and `HttpCurrentActor`.**

```csharp
namespace RuinaRPG.Infrastructure.SheetLogs;

/// <summary>Quem está fazendo a requisição atual; nulo fora de uma requisição autenticada.</summary>
public interface ICurrentActor
{
    Guid? UserId { get; }
}
```

```csharp
namespace RuinaRPG.Api.Services;

public sealed class HttpCurrentActor(IHttpContextAccessor accessor) : ICurrentActor
{
    public Guid? UserId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;
}
```

`Program.cs`: `builder.Services.AddHttpContextAccessor();`, `builder.Services.AddScoped<ICurrentActor, HttpCurrentActor>();`, `builder.Services.AddScoped<SheetLogInterceptor>();`, and change the context registration to

```csharp
builder.Services.AddDbContext<RuinaRpgDbContext>((sp, options) => options
    .UseNpgsql(builder.Configuration.GetConnectionString("Postgres"))
    .AddInterceptors(sp.GetRequiredService<SheetLogInterceptor>()));
```

Check how `ApiFactory` overrides the connection string in tests: if it replaces the `DbContextOptions` registration, the interceptor must survive that override (attach it there too, the same way).

- [ ] **Step 4: `SheetLogInterceptor`.** Override `SavingChangesAsync` (and `SavingChanges`, delegating to the async one with `.GetAwaiter().GetResult()`). Algorithm:

```
actor = currentActor.UserId; if null → return
db = eventData.Context as RuinaRpgDbContext; if null → return
entries = db.ChangeTracker.Entries().Where(e => SheetLogMap.Entidades.ContainsKey(e.Metadata.ClrType)
              && e.State is Added or Modified or Deleted).ToList()        // snapshot BEFORE adding log rows
if none → return

newSheets = ids of sheet rows in state Added
for each entry:
    map = SheetLogMap.Entidades[type]
    (kind, sheetId) = resolve:
        map.IsSheet            → entry's Id
        map.SheetIdProperty    → that property's current (or original, when Deleted) value
        map.ParentType         → the parent's sheet id: from the tracked parent entry if present,
                                 else one query on the parent table by map.ParentIdProperty
    if sheetId in newSheets and !map.IsSheet → skip        // creation is one line
    if kind is Npc or Creature and the sheet is not granted (OwnerId null) → skip
        (OwnerId: from the tracked sheet entry if present, else one query; cache per sheet id for this save)

    Added   & IsSheet  → one entry Acao=Criou, Secao=map.Secao
    Added   & child    → Acao=Adicionou, ItemNome=row name
    Deleted & IsSheet  → nothing (the FK cascade removes the log)
    Deleted & child    → Acao=Removeu, ItemNome=row name (from ORIGINAL values)
    Modified           → for each property p with entry.Property(p).IsModified
                               and !Equals(OriginalValue, CurrentValue)
                               and p not in map.Ignorados:
                             p in TextosLongos → Acao=Alterou, Campo=label, values null
                             else              → Acao=Alterou, Campo=map.Campos[p] (or p itself if unlabelled),
                                                 ValorAntes/ValorDepois rendered, Categoria=map.Categorias[p] or Geral,
                                                 ItemNome=row name for child rows

spell effects (entities whose map has ParentType): never produce their own line.
    Collect the parent ids that had any effect Added/Modified/Deleted.
    For a parent that is itself Added or Deleted in this save → nothing extra.
    Otherwise → one entry Acao=Alterou, Secao="Magias & Habilidades", ItemNome=parent's Nome, Campo="Efeitos", values null.

for every produced entry: OccurredAt = one DateTime.UtcNow taken once per save;
    ActorUserId, ActorNickname (one query on Users per save), ActorIsGm
        (Character: actor == GmId of the sheet's campaign; Npc/Creature: actor == sheet.GmId; cached per sheet)
    set exactly one of CharacterSheetId / NpcSheetId / CreatureSheetId
db.SheetLogEntries.AddRange(produced)
```

Row names and reference values:
- `ItemNomeProperty` → `SheetLogValues.Render(value)`.
- `ItemRefProperty` and reference-valued fields → resolve the name with one query per distinct (table, id) per save: `ItemId`/`ArtifactItemId` → `db.Items` `Nome`; `PericiaId` → `db.Pericias` name; `TraitId` → `db.Traits` `Nome`, or `CreatureExclusiveTraitId` → `db.Set<CreatureExclusiveTrait>()` `Nome`; `HistoricoId` → `db.Historicos` name; `EquipmentKitId` → `db.EquipmentKits` name; `ImageId` → the literal "imagem". A reference that no longer resolves renders as "(removido)". A null reference renders as "—".
- Queries inside the interceptor use `AsNoTracking()` and must not add anything but log rows to the tracker.

Keep the class focused: the property-diff loop, the sheet/grant resolution and the name resolution are three private methods. If the file passes ~250 lines, split the name resolution into `SheetLogNames.cs` in the same folder.

- [ ] **Step 5: Test seam for test 14.** Register in that test's scope a stub `ICurrentActor` returning the GM's id and build the context through DI so the interceptor is attached (e.g. `_factory.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddScoped<ICurrentActor>(_ => new FixedActor(gmId))))`). Do not add test-only code to production classes.

- [ ] **Step 6: Run** `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~SheetLogRecordingTests` → PASS. Then, one at a time, the classes most exposed to a change in every save: `CharacterSheetsControllerTests`, `NpcSheetsControllerTests`, `CampaignGrantsControllerTests`, the three `*SpellAbilitiesControllerTests`, `EquipagemControllerTests`-like classes (grep the Controllers test folder for `Equipagem`), and every class under `tests/RuinaRPG.Tests.Integration/Persistence` → PASS.

- [ ] **Step 7: Docs.** `Requisitos - Técnico.md`: a short requirement (next free `R00NN`) — the sheet log is written by a single EF Core save interceptor; a new sheet table or column must be added to `SheetLogMap` (a test enforces it); bulk `ExecuteUpdate`/`ExecuteDelete` bypass it, and the one existing use on sheet tables (Auditor removing a Perícia) is intentionally unlogged.

- [ ] **Step 8: Commit** — `feat(api): interceptor grava o log de ficha a cada salvamento`.

---

### Task 5: Read endpoint

**Files:**
- Create: `src/RuinaRPG.Contracts/SheetLogs/SheetLogEntryResponse.cs`, `SheetLogPageResponse.cs`
- Create: `src/RuinaRPG.Api/Controllers/SheetLogsController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/SheetLogsControllerTests.cs`

**Interfaces — produces:**

```csharp
public record SheetLogEntryResponse(string Id, DateTime OccurredAt, string ActorNickname, bool ActorIsGm,
    string Categoria, string Acao, string Secao, string? Campo, string? ItemNome, string? ValorAntes, string? ValorDepois);
public record SheetLogPageResponse(List<SheetLogEntryResponse> Itens, string? ProximoCursor);
```

`GET api/sheet-logs/{kind}/{sheetId}?de=&ate=&recursos=&estresse=&antesDe=&limite=`
- `kind` ∈ `character` | `npc` | `creature` (anything else → 404).
- Authorization: `character` → caller is the `GmId` of the sheet's campaign; `npc`/`creature` → caller is the sheet's `GmId`. Otherwise, and for an unknown sheet, **404**.
- `de` inclusive, `ate` exclusive, both UTC instants; `recursos=false` excludes `RecursoVFA`; `estresse=false` excludes `Estresse`; both default `true`.
- Order: `OccurredAt` desc, then `Id` desc. `antesDe` is the cursor of the last row of the previous page, formatted `"{OccurredAt.Ticks}_{Id}"`; a malformed cursor → 400. `limite` default 100, clamped to 1..500.
- `ProximoCursor` is null when the page is the last one (fetch `limite + 1` rows to know).
- `OccurredAt` is returned as UTC (`DateTimeKind.Utc`).

- [ ] **Step 1: Failing tests**, creating log rows by doing real edits through the API (and, for date and paging cases, inserting `SheetLogEntry` rows directly with chosen `OccurredAt`):
  1. GM of the campaign gets 200 with the entries newest first.
  2. The owning player gets 404; another GM gets 404; an unknown sheet gets 404; `kind=xyz` gets 404.
  3. `npc` and `creature`: the sheet's GM gets 200; the granted player gets 404.
  4. `de`/`ate` window: rows at 10:00, 12:00, 14:00 UTC; `de=11:00&ate=14:00` returns only the 12:00 row (`ate` exclusive).
  5. `recursos=false` hides Vitalidade/Foco/Adrenalina lines and keeps Estresse and Geral; `estresse=false` hides Estresse only; both false hides both.
  6. Paging (Review Focus 4): insert 250 rows, three of them sharing one `OccurredAt`; `limite=100` three times following `ProximoCursor` returns 100, 100, 50 with no duplicates and no gaps, and the last `ProximoCursor` is null.
  7. `limite=0` behaves as 1; `limite=9999` as 500; `antesDe=lixo` → 400.
  8. Filters apply before paging: with `recursos=false` and 150 non-resource rows among 300, two pages return exactly those 150.

- [ ] **Step 2: Run, see 404s.**

- [ ] **Step 3: Implement** the controller in the style of the existing ones (`[ApiController]`, `[Authorize]`, primary-constructor `RuinaRpgDbContext db`, `CurrentUserId()` helper). The keyset predicate:

```csharp
query = query.Where(l => l.OccurredAt < cursorAt || (l.OccurredAt == cursorAt && l.Id.CompareTo(cursorId) < 0));
```

If Npgsql cannot translate `Guid.CompareTo`, order and compare by `OccurredAt` then `Id` using `EF.Functions` or a raw comparison the provider supports, and keep test 6 as the judge.

- [ ] **Step 4: Run the class → PASS. Build → 0 warnings.**

- [ ] **Step 5: Commit** — `feat(api): consulta paginada do log de ficha, só para o GM`.

---

### Task 6: The popup and the icons

**Files:**
- Create: `src/RuinaRPG.Client/Shared/LogDaFichaDialog.razor`, `src/RuinaRPG.Client/Shared/LogDaFichaAgrupador.cs`
- Modify: `src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor` (Membros list ~line 53-62; "Concessões existentes" ~line 237-246)
- Modify: `Docs/Requisitos/Requisitos - Campanha.md` (new **R0016**)
- Test: `tests/RuinaRPG.Tests.Client/Shared/LogDaFichaAgrupadorTests.cs`, `LogDaFichaDialogTests.cs`; `tests/RuinaRPG.Tests.Client/Pages/CampanhaDetalheTests.cs`

**Interfaces:**
- Consumes: the Task 5 endpoint and records.
- Produces: `LogDaFichaDialog` with parameters `string Kind` (`character`/`npc`/`creature`), `string SheetId`, `string? NomeDaFicha`.
  `static class LogDaFichaAgrupador`: `List<(DateOnly Dia, List<SheetLogEntryResponse> Linhas)> PorDia(IEnumerable<SheetLogEntryResponse> itens, TimeZoneInfo fuso)` and `string Texto(SheetLogEntryResponse linha)`.

- [ ] **Step 1: Failing unit tests for `LogDaFichaAgrupador`.**
  - `PorDia` (Review Focus 5): with a fixed zone of UTC−3, entries at `02:30Z` and `03:10Z` of the same UTC date fall in **different** local days (23:30 the day before, 00:10); groups come newest day first, lines newest first inside a group.
  - `Texto`:
    - `Alterou` with values → `"Vitalidade Atual: 30 → 25"`; with `ItemNome` → `"Força — Gasto: 2 → 3"`.
    - `Alterou` with null values → `"História: alterado"`; with `ItemNome` → `"Bola de Fogo — Efeitos: alterado"`.
    - `Adicionou` → `"adicionou Rival"`; `Removeu` → `"removeu Rival"`; `Criou` → `"ficha criada"`.

- [ ] **Step 2: Implement `LogDaFichaAgrupador`** (pure C#, no component dependencies) → tests PASS.

- [ ] **Step 3: Failing dialog tests** (harness as in `CaracteristicaDeCriaturaDialogTests`: `Render<MudDialogProvider>()` + `IDialogService.ShowAsync`):
  1. On open it requests `sheet-logs/character/s1` with `recursos=true&estresse=true` and no date bounds, and renders one day header per local day with the lines' text, time and actor; a line with `ActorIsGm` shows a "GM" chip.
  2. Unchecking "Vitalidade, Foco e Adrenalina" issues a new request with `recursos=false` and replaces the list; unchecking "Estresse" → `estresse=false`.
  3. Picking a date range issues a request whose `de` is the local start of the first day and `ate` the local start of the day after the last, both sent as UTC ISO strings.
  4. With a non-null `ProximoCursor` a "Carregar mais" button shows; clicking it requests `antesDe={cursor}` and **appends**; with a null cursor the button is absent.
  5. An empty result shows "Nenhuma alteração registrada neste período."
  6. A failed request shows an error alert and no list.

- [ ] **Step 4: Implement the dialog.** `MudDialog` with: title "Log da ficha — {NomeDaFicha}"; a `MudDateRangePicker` (label "Período", clearable); two `MudCheckBox` ("Vitalidade, Foco e Adrenalina", "Estresse"), both initially checked; the list grouped with `LogDaFichaAgrupador.PorDia(_itens, TimeZoneInfo.Local)`, each day as a `MudText Typo.subtitle1` header (`dddd, dd/MM/yyyy` in pt-BR) and each line showing local time `HH:mm`, the actor nickname, a small "GM" `MudChip` when applicable, the `Secao` in secondary color and `LogDaFichaAgrupador.Texto(linha)`; "Carregar mais"; a "Fechar" action. Changing any filter clears the list and reloads from the first page. Page size: the endpoint default.

- [ ] **Step 5: Failing page tests** in `CampanhaDetalheTests`: with one member sheet and one grant stubbed, each row renders an icon button with `aria-label="Log da ficha"`; with no sheets and no grants there is none.

- [ ] **Step 6: Add the icons.** In the Membros list, after the "— Nível …" `MudText`:

```razor
                                        <MudTooltip Text="Log da ficha">
                                            <MudIconButton Icon="@Icons.Material.Filled.History" Size="Size.Small" aria-label="Log da ficha"
                                                           OnClick="@(() => AbrirLogAsync("character", sheet.Id, sheet.Nome))" />
                                        </MudTooltip>
```

In "Concessões existentes", after "— concedida a …", the same button with `AbrirLogAsync(grant.Tipo == "Npc" ? "npc" : "creature", grant.SheetId, grant.Nome)`.

```csharp
    private Task AbrirLogAsync(string kind, string sheetId, string? nome) =>
        Dialogs.ShowAsync<LogDaFichaDialog>(null,
            new DialogParameters<LogDaFichaDialog> { { x => x.Kind, kind }, { x => x.SheetId, sheetId }, { x => x.NomeDaFicha, nome ?? "(sem nome)" } },
            new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, CloseButton = true });
```

Inject `IDialogService Dialogs` if the page does not have it. Clicking the icon must not navigate to the sheet (the icon is outside the `MudLink`).

- [ ] **Step 7: Docs.** `Requisitos - Campanha.md`, new requirement after R0015:

`# **R0016** - O GM consulta o log de alterações de cada ficha.`

`**Descrição**: Na lista de fichas de cada membro (aba Membros) e em cada linha de "Concessões existentes", um ícone ao lado da ficha — depois de "Nível x, Vocação (Linhagem)" — abre um popup com o log daquela ficha: todas as alterações feitas nela, pelo jogador ou pelo próprio GM, cada uma com data e hora, quem fez, a seção, o campo e o valor antes e depois. Textos longos (História, descrições) aparecem apenas como "alterado", sem o conteúdo; o diário do personagem não entra no log. As linhas vêm agrupadas por dia, da mais recente para a mais antiga, e o GM pode filtrar por período. Duas caixas de seleção, marcadas por padrão, mostram ou escondem as linhas de **Vitalidade, Foco e Adrenalina** atuais e, à parte, as de **Estresse** atual. O log vale para toda Ficha de Personagem e para as Fichas de NPC e de Criatura concedidas a um jogador (R0010); fichas de NPC/Criatura não concedidas não têm log. Só o GM vê o log — o jogador não tem acesso, nem ao da própria ficha. O registro começa na versão em que o recurso foi introduzido (não há histórico anterior) e é guardado enquanto a ficha existir. Ver "[[Requisitos - Modelo de Dados]]" (SheetLogEntries).`

Use the real number of the grant requirement if it is not R0010 (check the doc).

- [ ] **Step 8: Verify** — `dotnet build` (0 warnings), `dotnet test tests/RuinaRPG.Tests.Unit`, `dotnet test tests/RuinaRPG.Tests.Client`.

- [ ] **Step 9: Commit and push** — `feat(client): popup do log de ficha na página da campanha`.
