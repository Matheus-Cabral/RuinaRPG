# Passivas Coringa e aba Habilidades Passivas — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a wildcard Passiva column to the Tabela de Níveis (a slot that accepts a Passiva of any category) and a logged-in-only "Habilidades Passivas" tab to the Livro de Regras; ship as 1.4.2.

**Architecture:** The wildcard is a new system column (`MaxPassivasCoringa`) and a pure Domain rule in `LimitesDeNivel` — nothing is stored per Passiva. The four Passiva columns get a fixed player-facing label, defined once in `ChavesDeNivel`, used by the bonus lines and the sheet panel. The Livro tab is a client component fed by one new authenticated endpoint that returns ready-to-render rows (requisitos already written out as text).

**Tech Stack:** .NET 8, ASP.NET Core Web API, EF Core/Npgsql, Blazor WebAssembly + MudBlazor, xUnit + FluentAssertions, bUnit, Testcontainers PostgreSQL.

**Spec:** `docs/superpowers/specs/2026-10-03-passivas-coringa-e-aba-passivas-design.md`

## Global Constraints

- Branch `release/1.4.2`. Commit after every task; push after every task (`git push`).
- `dotnet build` must finish with **0 warnings, 0 errors**.
- TDD is mandatory (Técnico R0011): failing test first, then the minimum code.
- Integration tests need a running Docker daemon. They have a known ~2-3% timeout flake: a failed test that passes when rerun in isolation (`--filter FullyQualifiedName~Name`) is the flake, not a regression.
- No EF Core schema migration in this plan (no entity changes).
- UI text is Brazilian Portuguese. Icons are MudBlazor icons, never emojis.
- Player-facing labels (every page that is not an Auditoria page): `MaxPassivasCoringa` → **"Habilidade Passiva"**, `MaxPassivasLivres` → **"Passiva Livre"**, `MaxPassivasVocacionais` → **"Passiva Vocacional"**, `MaxPassivasDeClasse` → **"Passiva de Classe"**. Auditoria pages show the column name (default for the new one: **"Passivas Coringa"**).
- Follow the surrounding code's comment density and naming (Portuguese domain names, comments in the language of the file).
- Commit messages in Portuguese, conventional prefix (`feat:`, `fix:`, `test:`, `docs:`), ending with:
  ```
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01Wfw422FV8DM1iu5hCUwBS1
  ```

## Review Focus

- **Sheet already above its limits** (table edited afterwards): adding a Passiva in a category that still has room must be accepted even if another category's excess already exceeds the wildcards. (Task 2 unit test.)
- **Category with an entirely empty column** while wildcards are exhausted: adding to it must be accepted and must not consume a wildcard. (Task 2 unit test.)
- **Database seeded before 1.4.2 where the Auditor reordered or added columns**: the new column must land right after "Passivas De Classe" without breaking the relative order of the others. (Task 3 integration test.)
- **Jogador asking for a campaign they are not a member of, or with no `campaignId`**: 403 / 400, never another GM's Passivas. (Task 4 integration tests.)
- **Passiva whose requisito points at a removed Perícia or Histórico**: the tab still renders; the removed Perícia is omitted, the Histórico shows "(removido)". (Task 4 unit + integration.)

---

### Task 1: Coluna Coringa e rótulos fixos das colunas de Passiva

**Files:**
- Modify: `src/RuinaRPG.Domain/Rules/Niveis/ChavesDeNivel.cs`
- Modify: `src/RuinaRPG.Domain/Rules/Niveis/ProgressaoDeNivel.cs` (`LinhasDeBonus`, `LinhasDeBonusAcumuladas`)
- Modify: `src/RuinaRPG.Client/Shared/ProgressaoDoNivelSection.razor`
- Test: `tests/RuinaRPG.Tests.Unit/Rules/ProgressaoDeNivelTests.cs`
- Test: `tests/RuinaRPG.Tests.Client/Shared/ProgressaoDoNivelSectionTests.cs`

**Interfaces:**
- Produces: `ChavesDeNivel.MaxPassivasCoringa` (const string `"MaxPassivasCoringa"`); `ChavesDeNivel.RotuloParaJogador(string? chaveDeSistema, string nomeDaColuna)` → `string`; `ChavesDeNivel.Sistema` now has 14 entries with Coringa at `Ordem` 11, XP 12, EAP 13.

- [ ] **Step 1: Write the failing unit tests** — append to `ProgressaoDeNivelTests`:

```csharp
    [Fact]
    public void The_wildcard_passiva_column_is_additive_and_sits_right_after_De_Classe()
    {
        var sistema = ChavesDeNivel.Sistema.OrderBy(d => d.Ordem).Select(d => d.Chave).ToList();
        var coringa = ChavesDeNivel.Sistema.Single(d => d.Chave == ChavesDeNivel.MaxPassivasCoringa);

        coringa.Should().Match<ChavesDeNivel.Definicao>(d => d.Tipo == TipoDeColunaDeNivel.Acumulativa && d.Nome == "Passivas Coringa");
        sistema.IndexOf(ChavesDeNivel.MaxPassivasCoringa).Should().Be(sistema.IndexOf(ChavesDeNivel.MaxPassivasDeClasse) + 1);
        sistema.Should().EndWith(new[] { ChavesDeNivel.XpParaProximoNivel, ChavesDeNivel.EapBase });
        ChavesDeNivel.Sistema.Select(d => d.Ordem).Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(ChavesDeNivel.MaxPassivasCoringa, "Habilidade Passiva")]
    [InlineData(ChavesDeNivel.MaxPassivasLivres, "Passiva Livre")]
    [InlineData(ChavesDeNivel.MaxPassivasVocacionais, "Passiva Vocacional")]
    [InlineData(ChavesDeNivel.MaxPassivasDeClasse, "Passiva de Classe")]
    public void RotuloParaJogador_is_fixed_for_the_passiva_columns_whatever_the_column_name(string chave, string rotulo) =>
        ChavesDeNivel.RotuloParaJogador(chave, "Nome dado pelo Auditor").Should().Be(rotulo);

    [Theory]
    [InlineData(ChavesDeNivel.PontosDeAtributo)]
    [InlineData(null)]
    public void RotuloParaJogador_is_the_column_name_for_every_other_column(string? chave) =>
        ChavesDeNivel.RotuloParaJogador(chave, "Nome dado pelo Auditor").Should().Be("Nome dado pelo Auditor");

    [Fact]
    public void Bonus_lines_use_the_fixed_passiva_labels_not_the_column_names()
    {
        var t = TabelaDeNiveisDeTeste.Criar(
            TabelaDeNiveisDeTeste.Nivel(1, (ChavesDeNivel.MaxPassivasLivres, 1)),
            TabelaDeNiveisDeTeste.Nivel(2, (ChavesDeNivel.MaxPassivasCoringa, 1), (ChavesDeNivel.MaxPassivasDeClasse, 1)));

        t.LinhasDeBonus(1).Should().Equal("Passiva Livre: +1");
        t.LinhasDeBonus(2).Should().Equal("Passiva de Classe: +1", "Habilidade Passiva: +1");
        t.LinhasDeBonusAcumuladas(0, 2).Should().Equal("Passiva Livre: +1", "Passiva de Classe: +1", "Habilidade Passiva: +1");
    }
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter FullyQualifiedName~ProgressaoDeNivelTests`
Expected: build error — `ChavesDeNivel.MaxPassivasCoringa` / `RotuloParaJogador` do not exist.

- [ ] **Step 3: Implement in `ChavesDeNivel.cs`**

Add the constant to the `const string` list (same line as the other Passiva keys):

```csharp
        MaxPassivasLivres = "MaxPassivasLivres", MaxPassivasVocacionais = "MaxPassivasVocacionais", MaxPassivasDeClasse = "MaxPassivasDeClasse",
        MaxPassivasCoringa = "MaxPassivasCoringa",
```

Replace the tail of `Sistema`:

```csharp
        new Definicao(MaxPassivasDeClasse, "Passivas De Classe", TipoDeColunaDeNivel.Acumulativa, 10),
        new Definicao(MaxPassivasCoringa, "Passivas Coringa", TipoDeColunaDeNivel.Acumulativa, 11),
        new Definicao(XpParaProximoNivel, "XP para o próximo nível", TipoDeColunaDeNivel.PorNivel, 12),
        new Definicao(EapBase, "EAP base", TipoDeColunaDeNivel.PorNivel, 13),
```

Add below `MaxPassivas`:

```csharp
    /// <summary>
    /// Nome da coluna fora da Auditoria (ficha, aviso de subida de nível, Livro de Regras). As quatro colunas
    /// de Passiva têm rótulo fixo — a Coringa é só "Habilidade Passiva" —; as demais usam o nome da coluna.
    /// </summary>
    public static string RotuloParaJogador(string? chaveDeSistema, string nomeDaColuna) => chaveDeSistema switch
    {
        MaxPassivasCoringa => "Habilidade Passiva",
        MaxPassivasLivres => RequisitoLabels.Categoria(CategoriaDePassiva.Livre),
        MaxPassivasVocacionais => RequisitoLabels.Categoria(CategoriaDePassiva.Vocacional),
        MaxPassivasDeClasse => RequisitoLabels.Categoria(CategoriaDePassiva.DeClasse),
        _ => nomeDaColuna,
    };
```

- [ ] **Step 4: Use the label in `ProgressaoDeNivel.cs`**

In `LinhasDeBonus`: `.Select(x => $"{ChavesDeNivel.RotuloParaJogador(x.c.ChaveDeSistema, x.c.Nome)}: +{x.v}")`.
In `LinhasDeBonusAcumuladas`: `.Select(x => $"{ChavesDeNivel.RotuloParaJogador(x.c.ChaveDeSistema, x.c.Nome)}: +{x.soma}")`.

- [ ] **Step 5: Run the unit suite**

Run: `dotnet test tests/RuinaRPG.Tests.Unit`
Expected: PASS. If an existing test asserted a bonus line like `"Passivas Livres: +1"`, update that literal to the fixed label (`"Passiva Livre: +1"`) — that is the intended behaviour change. `TabelaDeNiveisHtmlTests` is the likely place.

- [ ] **Step 6: Write the failing client test** — in `ProgressaoDoNivelSectionTests`, replace the body assertions of `Passiva_columns_show_the_additive_limit_or_sem_limite_when_the_column_is_empty` and add a wildcard test:

```csharp
    [Fact]
    public async Task Passiva_columns_show_the_additive_limit_or_sem_limite_when_the_column_is_empty()
    {
        var livres = Guid.NewGuid();
        var vocacionais = Guid.NewGuid();
        var tabela = new TabelaDeNiveisResponse(
            [Col(livres, "Passivas Livres", "Acumulativa", ChavesDeNivel.MaxPassivasLivres, 1),
             Col(vocacionais, "Passivas Vocacionais", "Acumulativa", ChavesDeNivel.MaxPassivasVocacionais, 2)],
            [Linha(1, (livres, 1), (vocacionais, null)), Linha(2), Linha(3, (livres, null))]);

        var cut = await RenderAsync(tabela, 3);

        var linhas = cut.FindAll("tr").Select(r => r.TextContent.Trim()).ToList();
        linhas.Should().Contain(l => l.StartsWith("Passiva Livre") && l.EndsWith("1"));
        linhas.Should().Contain(l => l.StartsWith("Passiva Vocacional") && l.EndsWith("sem limite"));
        cut.Markup.Should().NotContain("Passivas Livres").And.NotContain("Passivas Vocacionais");
        cut.Markup.Should().NotContain("máx.");
    }

    [Fact]
    public async Task Wildcard_column_shows_as_Habilidade_Passiva_with_the_accumulated_count_and_zero_when_empty()
    {
        var coringa = Guid.NewGuid();
        var comValor = new TabelaDeNiveisResponse(
            [Col(coringa, "Passivas Coringa", "Acumulativa", ChavesDeNivel.MaxPassivasCoringa, 1)],
            [Linha(1, (coringa, 1)), Linha(2, (coringa, 1))]);

        var cut = await RenderAsync(comValor, 2);

        var linhas = cut.FindAll("tr").Select(r => r.TextContent.Trim()).ToList();
        linhas.Should().ContainSingle(l => l.StartsWith("Habilidade Passiva") && l.EndsWith("2"));
        cut.Markup.Should().NotContain("Passivas Coringa").And.NotContain("sem limite");
    }

    [Fact]
    public async Task Empty_wildcard_column_shows_zero_not_sem_limite()
    {
        var coringa = Guid.NewGuid();
        var vazia = new TabelaDeNiveisResponse(
            [Col(coringa, "Passivas Coringa", "Acumulativa", ChavesDeNivel.MaxPassivasCoringa, 1)],
            [Linha(1), Linha(2)]);

        var cut = await RenderAsync(vazia, 2);

        cut.FindAll("tr").Select(r => r.TextContent.Trim()).Should().ContainSingle(l => l.StartsWith("Habilidade Passiva") && l.EndsWith("0"));
        cut.Markup.Should().NotContain("sem limite");
    }
```

- [ ] **Step 7: Run to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~ProgressaoDoNivelSectionTests`
Expected: FAIL — the panel still prints the column name.

- [ ] **Step 8: Implement in `ProgressaoDoNivelSection.razor`**

Change the name cell: `<td>@ChavesDeNivel.RotuloParaJogador(coluna.ChaveDeSistema, coluna.Nome)</td>`.

`EhColunaDePassiva` stays restricted to the three category columns (the wildcard falls through to the Acumulativa branch, which prints `valor ?? 0`). Add a comment on it:

```csharp
    // Só as três de categoria: a Coringa é um saldo (vazia = 0), não um limite que pode ser "sem limite".
```

- [ ] **Step 9: Run client tests, build, commit**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~ProgressaoDoNivelSectionTests` → PASS.
Run: `dotnet build` → 0 warnings, 0 errors.

```bash
git add -A && git commit -m "feat: coluna Passivas Coringa e rótulos fixos das colunas de Passiva"   # + trailer lines
git push
```

---

### Task 2: Regra do limite com vagas coringa (Personagem e NPC)

**Files:**
- Modify: `src/RuinaRPG.Domain/Rules/Niveis/LimitesDeNivel.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSpellAbilitiesController.cs` (the block around line 77-83)
- Modify: `src/RuinaRPG.Api/Controllers/NpcSpellAbilitiesController.cs` (the block around line 89-94)
- Test: `tests/RuinaRPG.Tests.Unit/Rules/LimitesDeNivelTests.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/NivelLimitsTests.cs`

**Interfaces:**
- Consumes: `ChavesDeNivel.MaxPassivasCoringa`, `ChavesDeNivel.MaxPassivas(CategoriaDePassiva)`, `ProgressaoDeNivel.LimiteAcumulado(string chave, int nivel)` → `int?`.
- Produces: `LimitesDeNivel.Passivas(CategoriaDePassiva categoria, IReadOnlyCollection<CategoriaDePassiva> naFicha, ProgressaoDeNivel tabela, int nivel)` → `string?` (null = allowed). **Replaces** the old `Passivas(categoria, jaNaFicha, limite, nivel)` overload, which is deleted.

- [ ] **Step 1: Write the failing unit tests** — in `LimitesDeNivelTests`, delete the two old `Passivas` tests (`Passivas` theory and `Passivas_message_uses_the_category_label`) and add:

```csharp
    private static readonly CategoriaDePassiva Livre = CategoriaDePassiva.Livre, Vocacional = CategoriaDePassiva.Vocacional, DeClasse = CategoriaDePassiva.DeClasse;

    /// <summary>Tabela de um nível só; limite nulo = coluna inteiramente vazia (sem limite).</summary>
    private static ProgressaoDeNivel Tabela(int? livres = null, int? vocacionais = null, int? deClasse = null, int? coringas = null)
    {
        var valores = new List<(string, int)>();
        if (livres is { } l) valores.Add((ChavesDeNivel.MaxPassivasLivres, l));
        if (vocacionais is { } v) valores.Add((ChavesDeNivel.MaxPassivasVocacionais, v));
        if (deClasse is { } c) valores.Add((ChavesDeNivel.MaxPassivasDeClasse, c));
        if (coringas is { } k) valores.Add((ChavesDeNivel.MaxPassivasCoringa, k));
        return TabelaDeNiveisDeTeste.Criar(TabelaDeNiveisDeTeste.Nivel(1, valores.ToArray()));
    }

    private static bool Permite(CategoriaDePassiva categoria, CategoriaDePassiva[] naFicha, ProgressaoDeNivel tabela) =>
        LimitesDeNivel.Passivas(categoria, naFicha, tabela, 1) is null;

    [Fact]
    public void Passivas_without_wildcards_keep_the_per_category_limit()
    {
        Permite(Livre, [], Tabela(livres: 1)).Should().BeTrue();
        Permite(Livre, [Livre], Tabela(livres: 1)).Should().BeFalse();
        Permite(Livre, [], Tabela(livres: 0)).Should().BeFalse();
        Permite(Vocacional, [Livre], Tabela(livres: 1, vocacionais: 1)).Should().BeTrue();
    }

    [Fact]
    public void Passivas_of_a_category_with_an_entirely_empty_column_are_unlimited_and_never_use_a_wildcard()
    {
        Permite(Livre, [Livre, Livre, Livre], Tabela()).Should().BeTrue();
        // Vocacional já gastou a única coringa; Livre (coluna vazia) segue liberada.
        Permite(Livre, [Vocacional, Livre, Livre], Tabela(vocacionais: 0, coringas: 1)).Should().BeTrue();
        // Passivas de categoria sem limite não contam como excedente.
        Permite(Vocacional, [Livre, Livre, Livre], Tabela(vocacionais: 0, coringas: 1)).Should().BeTrue();
    }

    [Fact]
    public void A_wildcard_slot_accepts_a_passiva_of_any_category_once_its_own_limit_is_full()
    {
        var t = Tabela(livres: 1, vocacionais: 0, deClasse: 0, coringas: 1);
        Permite(Livre, [Livre], t).Should().BeTrue();
        Permite(Vocacional, [Livre], t).Should().BeTrue();
        Permite(DeClasse, [Livre], t).Should().BeTrue();
    }

    [Fact]
    public void Wildcards_are_shared_across_categories_and_run_out()
    {
        var t = Tabela(livres: 1, vocacionais: 0, deClasse: 0, coringas: 1);
        Permite(Livre, [Livre, Vocacional], t).Should().BeFalse();    // a coringa já foi para a Vocacional
        Permite(DeClasse, [Livre, Livre], t).Should().BeFalse();      // a coringa já foi para a segunda Livre
        Permite(DeClasse, [Livre, Livre], Tabela(livres: 1, deClasse: 0, coringas: 2)).Should().BeTrue();
    }

    [Fact]
    public void A_sheet_already_above_its_limits_can_still_add_where_the_category_has_room()
    {
        // Três Livres com limite 1 e nenhuma coringa (a tabela foi editada depois): Vocacional ainda cabe.
        var t = Tabela(livres: 1, vocacionais: 1, coringas: 0);
        Permite(Vocacional, [Livre, Livre, Livre], t).Should().BeTrue();
        Permite(Livre, [Livre, Livre, Livre], t).Should().BeFalse();
    }

    [Theory]
    [InlineData(CategoriaDePassiva.Livre, "Livre(s)")]
    [InlineData(CategoriaDePassiva.Vocacional, "Vocacional(is)")]
    [InlineData(CategoriaDePassiva.DeClasse, "De Classe")]
    public void Passivas_message_without_wildcards_is_the_category_limit(CategoriaDePassiva categoria, string rotulo)
    {
        var t = Tabela(livres: 2, vocacionais: 2, deClasse: 2);
        LimitesDeNivel.Passivas(categoria, [categoria, categoria], t, 1).Should().Be($"O nível 1 permite no máximo 2 Passiva(s) {rotulo}.");
    }

    [Fact]
    public void Passivas_message_with_wildcards_says_they_are_used_up() =>
        LimitesDeNivel.Passivas(Livre, [Livre, Livre], Tabela(livres: 1, coringas: 1), 1)
            .Should().Be("O nível 1 permite no máximo 1 Passiva(s) Livre(s), e as 1 vaga(s) coringa já estão em uso.");
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter FullyQualifiedName~LimitesDeNivelTests`
Expected: build error — no `Passivas` overload takes `(CategoriaDePassiva, CategoriaDePassiva[], ProgressaoDeNivel, int)`.

- [ ] **Step 3: Implement in `LimitesDeNivel.cs`** — replace the old `Passivas` method:

```csharp
    /// <summary>
    /// Limite de Passivas ao adicionar uma de <paramref name="categoria"/>. Cada categoria tem o próprio limite
    /// (coluna inteiramente vazia = sem limite); o que passa dele ocupa vagas da coluna Passivas Coringa, que
    /// servem a qualquer categoria. Nada é marcado na Passiva: a conta é refeita a cada adição.
    /// </summary>
    public static string? Passivas(CategoriaDePassiva categoria, IReadOnlyCollection<CategoriaDePassiva> naFicha, ProgressaoDeNivel tabela, int nivel)
    {
        int? Limite(CategoriaDePassiva c) => tabela.LimiteAcumulado(ChavesDeNivel.MaxPassivas(c), nivel);

        // Sem limite na categoria, ou ainda dentro dele: não precisa de coringa.
        if (Limite(categoria) is not { } limite || naFicha.Count(c => c == categoria) < limite)
            return null;

        var coringas = tabela.LimiteAcumulado(ChavesDeNivel.MaxPassivasCoringa, nivel) ?? 0;
        var emUso = Enum.GetValues<CategoriaDePassiva>()
            .Sum(c => Limite(c) is { } l ? Math.Max(0, naFicha.Count(x => x == c) - l) : 0);
        if (emUso < coringas)
            return null;

        var daCategoria = $"O nível {nivel} permite no máximo {limite} Passiva(s) {Rotulo(categoria)}";
        return coringas == 0 ? daCategoria + "." : $"{daCategoria}, e as {coringas} vaga(s) coringa já estão em uso.";
    }
```

Update the class summary comment's "Passivas" mention only if it becomes inaccurate (it does not).

- [ ] **Step 4: Run the unit tests**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter FullyQualifiedName~LimitesDeNivelTests`
Expected: PASS (the Api project does not compile yet — the unit project does not reference it).

- [ ] **Step 5: Write the failing integration tests** — append to `NivelLimitsTests` (after `Npc_passiva_cap_is_enforced_too`):

```csharp
    [Fact]
    public async Task Wildcard_slot_accepts_any_category_and_runs_out()
    {
        var (gm, sheetId, _) = await SetUpCharacterAsync("W1");
        var livre1 = await CreatePassivaAsync(gm, "Livre Um", "Livre");
        var livre2 = await CreatePassivaAsync(gm, "Livre Dois", "Livre");
        var vocacional = await CreatePassivaAsync(gm, "Vocacional Um", "Vocacional");
        Task<HttpResponseMessage> Add(string id) => _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", gm,
            new AddCharacterSpellAbilityRequest(id, null, null, null, null, null)));

        var livres = await SetLimiteAsync("MaxPassivasLivres", 1);
        var vocacionais = await SetLimiteAsync("MaxPassivasVocacionais", 0);
        var coringas = await SetLimiteAsync("MaxPassivasCoringa", 1);
        try
        {
            (await Add(livre1)).StatusCode.Should().Be(HttpStatusCode.Created);
            (await Add(vocacional)).StatusCode.Should().Be(HttpStatusCode.Created); // ocupa a vaga coringa
            var terceira = await Add(livre2);
            terceira.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await ReadBodyAsync(terceira)).Should().Contain("vaga(s) coringa já estão em uso");
        }
        finally
        {
            await SetLimiteAsync("MaxPassivasLivres", livres);
            await SetLimiteAsync("MaxPassivasVocacionais", vocacionais);
            await SetLimiteAsync("MaxPassivasCoringa", coringas);
        }
    }

    [Fact]
    public async Task Npc_wildcard_slot_is_honoured_too()
    {
        var gm = await RegisterGmAndGetTokenAsync("LimGmW2", "limgmw2@teste.com");
        var sheetId = await CreateNpcSheetAsync(gm);
        var livre1 = await CreatePassivaAsync(gm, "Livre Um", "Livre");
        var livre2 = await CreatePassivaAsync(gm, "Livre Dois", "Livre");
        var livre3 = await CreatePassivaAsync(gm, "Livre Três", "Livre");
        Task<HttpResponseMessage> Add(string id) => _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/spell-abilities", gm,
            new AddNpcSpellAbilityRequest(id, null, null, null, null, null)));

        var livres = await SetLimiteAsync("MaxPassivasLivres", 1);
        var coringas = await SetLimiteAsync("MaxPassivasCoringa", 1);
        try
        {
            (await Add(livre1)).StatusCode.Should().Be(HttpStatusCode.Created);
            (await Add(livre2)).StatusCode.Should().Be(HttpStatusCode.Created);
            (await Add(livre3)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            await SetLimiteAsync("MaxPassivasLivres", livres);
            await SetLimiteAsync("MaxPassivasCoringa", coringas);
        }
    }
```

The existing tests in this class (`Passiva_limit_is_additive_per_category_...`, `Passiva_column_entirely_empty_means_no_limit`, `Passiva_granted_only_above_the_sheet_level_...`, `Npc_passiva_cap_is_enforced_too`) cover "empty Coringa column = unchanged behaviour" and must keep passing untouched.

- [ ] **Step 6: Wire the controllers**

In `CharacterSpellAbilitiesController.cs`, replace the `if (bankEntry.Categoria is { } cat) { ... }` block:

```csharp
                if (bankEntry.Categoria is { } cat)
                {
                    var naFicha = await db.CharacterSpellAbilities
                        .Where(e => e.CharacterSheetId == sheetId && e.Tipo == SpellAbilityTipo.Passiva && e.Categoria != null)
                        .Select(e => e.Categoria!.Value).ToListAsync();
                    if (LimitesDeNivel.Passivas(cat, naFicha, await tabelaDeNiveis.ObterAsync(), sheet.Nivel) is { } erroDeLimite)
                        return BadRequest(erroDeLimite);
                }
```

In `NpcSpellAbilitiesController.cs`, the same with `db.NpcSpellAbilities` and `e.NpcSheetId == sheetId`:

```csharp
                if (bankEntry.Categoria is { } cat)
                {
                    var naFicha = await db.NpcSpellAbilities
                        .Where(e => e.NpcSheetId == sheetId && e.Tipo == SpellAbilityTipo.Passiva && e.Categoria != null)
                        .Select(e => e.Categoria!.Value).ToListAsync();
                    if (LimitesDeNivel.Passivas(cat, naFicha, await tabelaDeNiveis.ObterAsync(), sheet.Nivel) is { } erroDeLimite)
                        return BadRequest(erroDeLimite);
                }
```

If a `using` for `ChavesDeNivel` becomes unused in either file, leave the namespace import (other members of `RuinaRPG.Domain.Rules.Niveis` are still used).

- [ ] **Step 7: Run build + the integration class**

Run: `dotnet build` → 0 warnings, 0 errors.
Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~NivelLimitsTests`
Expected: PASS (all, old and new).

- [ ] **Step 8: Commit and push**

```bash
git add -A && git commit -m "feat: vagas coringa no limite de Passivas de Personagem e NPC"   # + trailer lines
git push
```

---

### Task 3: Seeder posiciona a coluna nova em bancos já semeados

**Files:**
- Modify: `src/RuinaRPG.Infrastructure/Rules/Niveis/TabelaDeNiveisSeeder.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Rules/TabelaDeNiveisSeederTests.cs`

**Interfaces:**
- Consumes: `ChavesDeNivel.Sistema` (Task 1).
- Produces: no new API. Behaviour: a system column created on a table that already has columns is inserted right after the previous system column of `ChavesDeNivel.Sistema`, shifting every column at or after that position by one.

Context: the Auditor can reorder columns and create custom ones (`TabelaDeNiveisController`, `Ordem = max + 1`), so `Definicao.Ordem` is only right for a fresh table. Today the seeder creates a missing system column with `Ordem = def.Ordem`, which on an existing database collides with XP/EAP (old Ordem 11/12).

- [ ] **Step 1: Write the failing test** — append to `TabelaDeNiveisSeederTests`:

```csharp
    [Fact]
    public async Task A_new_system_column_lands_after_its_predecessor_on_an_already_seeded_table()
    {
        await using var db = NewDb();
        await db.Database.MigrateAsync();
        var rules = new RulesDataProvider();
        var md = RulesDataProvider.ReadResource("Tabela de Níveis.md");
        await TabelaDeNiveisSeeder.SeedAsync(db, md, rules.XpPorNivel, rules.EapPorNivel);

        // Simula um banco de antes da 1.4.2: sem a Coringa, com XP/EAP logo depois de De Classe e uma coluna do Auditor no fim.
        var original = await db.ColunasDeNivel.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Ordem);
        var custom = new ColunaDeNivel { Id = Guid.NewGuid(), Nome = "Fama", Tipo = TipoDeColunaDeNivel.Acumulativa, Ordem = 99 };
        try
        {
            db.ColunasDeNivel.Remove(await db.ColunasDeNivel.SingleAsync(c => c.ChaveDeSistema == ChavesDeNivel.MaxPassivasCoringa));
            var deClasse = await db.ColunasDeNivel.SingleAsync(c => c.ChaveDeSistema == ChavesDeNivel.MaxPassivasDeClasse);
            (await db.ColunasDeNivel.SingleAsync(c => c.ChaveDeSistema == ChavesDeNivel.XpParaProximoNivel)).Ordem = deClasse.Ordem + 1;
            (await db.ColunasDeNivel.SingleAsync(c => c.ChaveDeSistema == ChavesDeNivel.EapBase)).Ordem = deClasse.Ordem + 2;
            custom.Ordem = deClasse.Ordem + 3;
            db.ColunasDeNivel.Add(custom);
            await db.SaveChangesAsync();

            (await TabelaDeNiveisSeeder.SeedAsync(db, md, rules.XpPorNivel, rules.EapPorNivel)).Should().Be(1);

            await using var verify = NewDb();
            var ordem = (await verify.ColunasDeNivel.OrderBy(c => c.Ordem).ToListAsync()).Select(c => c.ChaveDeSistema ?? c.Nome).ToList();
            ordem.Should().OnlyHaveUniqueItems();
            ordem.SkipWhile(c => c != ChavesDeNivel.MaxPassivasDeClasse).Should().Equal(
                ChavesDeNivel.MaxPassivasDeClasse, ChavesDeNivel.MaxPassivasCoringa, ChavesDeNivel.XpParaProximoNivel, ChavesDeNivel.EapBase, "Fama");
            (await verify.ColunasDeNivel.Select(c => c.Ordem).ToListAsync()).Should().OnlyHaveUniqueItems();
        }
        finally
        {
            // A tabela é compartilhada com as outras classes de teste: devolve o estado semeado.
            await using var restore = NewDb();
            restore.ColunasDeNivel.RemoveRange(restore.ColunasDeNivel.Where(c => c.Id == custom.Id));
            foreach (var coluna in await restore.ColunasDeNivel.ToListAsync())
                if (original.TryGetValue(coluna.Id, out var o)) coluna.Ordem = o;
            await restore.SaveChangesAsync();
            await TabelaDeNiveisSeeder.SeedAsync(restore, md, rules.XpPorNivel, rules.EapPorNivel);
        }
    }
```

Note for the implementer: after the restore, the Coringa column is a *new* row (new Id) sitting after De Classe with XP/EAP shifted — equivalent to the fresh seed. `Seeding_twice_changes_nothing` asserts `ColunasDeNivel.Count == ChavesDeNivel.Sistema.Count`, which the `finally` preserves by removing the custom column.

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~TabelaDeNiveisSeederTests`
Expected: the new test FAILS (Coringa gets `Ordem` 11 and collides / lands out of place); the others pass.

- [ ] **Step 3: Implement in `TabelaDeNiveisSeeder.SeedAsync`** — replace the column loop (up to the first `SaveChangesAsync`):

```csharp
        var criadas = 0;
        // Todas, não só as de sistema: inserir uma coluna de sistema no meio desloca também as do Auditor.
        var todas = await db.ColunasDeNivel.ToListAsync();
        var colunas = todas.Where(c => c.ChaveDeSistema != null).ToList();
        ColunaDeNivel? anterior = null;
        foreach (var def in ChavesDeNivel.Sistema)
        {
            if (colunas.FirstOrDefault(c => c.ChaveDeSistema == def.Chave) is { } existente)
            {
                MigrarPassiva(existente, def);
                anterior = existente;
                continue;
            }
            // Logo depois da coluna de sistema anterior (o Auditor pode ter reordenado a tabela); numa
            // tabela nova isso é a própria def.Ordem e não há nada a deslocar.
            var ordem = anterior is null ? def.Ordem : anterior.Ordem + 1;
            foreach (var deslocada in todas.Where(c => c.Ordem >= ordem))
                deslocada.Ordem++;
            var nova = new ColunaDeNivel { Id = Guid.NewGuid(), Nome = def.Nome, Tipo = def.Tipo, ChaveDeSistema = def.Chave, Ordem = ordem };
            db.ColunasDeNivel.Add(nova);
            todas.Add(nova);
            colunas.Add(nova);
            anterior = nova;
            criadas++;
        }
        await db.SaveChangesAsync();
```

Update the class `<summary>` to mention it: add the sentence "Uma coluna de sistema nova entra logo depois da anterior da lista, deslocando as seguintes." If `ColunasDeNivel` has a global query filter hiding `IsDeleted` rows, add `.IgnoreQueryFilters()` to the `todas` query so soft-deleted columns are shifted too (check `RuinaRpgDbContext`; the `TabelaDeNiveisController` list filters `!c.IsDeleted` explicitly, which suggests there is no global filter).

- [ ] **Step 4: Run the seeder tests and the full integration class that depends on the seeded table**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~TabelaDeNiveisSeederTests|FullyQualifiedName~NivelLimitsTests|FullyQualifiedName~TabelaDeNiveis"`
Expected: PASS.

- [ ] **Step 5: Build, commit, push**

Run: `dotnet build` → 0 warnings, 0 errors.

```bash
git add -A && git commit -m "feat: seeder insere coluna de sistema nova depois da anterior em tabelas já semeadas"   # + trailer lines
git push
```

---

### Task 4: Endpoint das Passivas do Livro de Regras

**Files:**
- Modify: `src/RuinaRPG.Domain/SpellsAndAbilities/PassivaRequisitosEvaluator.cs` (new `Descrever`)
- Create: `src/RuinaRPG.Contracts/Rules/PassivaDoLivroResponse.cs`
- Create: `src/RuinaRPG.Api/Controllers/RulebookPassivasController.cs`
- Modify: `docs/superpowers/specs/2026-10-03-passivas-coringa-e-aba-passivas-design.md` (API subsection)
- Test: `tests/RuinaRPG.Tests.Unit/SpellsAndAbilities/PassivaRequisitosEvaluatorTests.cs`
- Create test: `tests/RuinaRPG.Tests.Integration/Controllers/RulebookPassivasControllerTests.cs`

**Interfaces:**
- Produces:
  - `PassivaRequisitosEvaluator.Descrever(RequisitosDePassiva? requisitos, string? nomeDoHistoricoExigido, Func<int, string?> nomeDaPericia)` → `IReadOnlyList<string>` (a Perícia whose name resolves to null is omitted).
  - `RuinaRPG.Contracts.Rules.PassivaDoLivroResponse(string Id, string Nome, string Categoria, string Descricao, List<string> Requisitos)` — `Categoria` is the `CategoriaDePassiva` enum name (`"Livre"`, `"Vocacional"`, `"DeClasse"`).
  - `GET /api/rulebook/passivas?campaignId={guid}` — `[Authorize]`. GM: own bank's Passivas, `campaignId` ignored. Jogador: 400 without `campaignId`, 404 unknown campaign, 403 non-member, otherwise the Passivas attached as public to that campaign. Sorted by Nome.

Design note (deviation from the spec's API subsection, to be recorded in the spec in Step 9): the spec had the client call the GM bank listing and a new `campaigns/{id}/passivas` returning `SpellAbilityEntryResponse`. That DTO carries a Histórico *Id* and Perícia *Chaves*, so the client would need two more catalog fetches just to print names. One endpoint returning ready text is smaller and keeps the labels in the Domain, next to the ones the sheet already uses for pendências.

- [ ] **Step 1: Write the failing unit tests** — append to `PassivaRequisitosEvaluatorTests` (add `using RuinaRPG.Domain.CharacterSheets;` if the file does not have it; reuse enum members that already appear in that file's tests if any name below does not exist):

```csharp
    [Fact]
    public void Descrever_is_empty_without_requisitos()
    {
        PassivaRequisitosEvaluator.Descrever(null, null, _ => "x").Should().BeEmpty();
        PassivaRequisitosEvaluator.Descrever(new RequisitosDePassiva(), null, _ => "x").Should().BeEmpty();
    }

    [Fact]
    public void Descrever_lists_every_filled_requisito_in_field_order_with_the_sheet_labels()
    {
        var requisitos = new RequisitosDePassiva
        {
            Nivel = 10, Vocacao = Vocacao.Campeao, Classe = " Paladino ", Linhagem = Linhagem.Econos, Graduacao = 3, CoracaoDeMana = true,
            Afinidade = AfinidadeElemental.Agua, HistoricoId = Guid.NewGuid(),
            Atributos = [new RequisitoDeAtributo(Atributo.Forca, 4)],
            SubAtributos = [new RequisitoDeSubAtributo(SubAtributo.Movimentacao, 6)],
            Pericias = [new RequisitoDePericia(7, 2)],
        };

        PassivaRequisitosEvaluator.Descrever(requisitos, "Nobre", id => id == 7 ? "Atletismo" : null).Should().Equal(
            "Nível 10", "Vocação: Campeão", "Classe: Paladino", "Linhagem: Ecônos", "Grau/Círculo 3", "Coração de Mana",
            "Afinidade: Água", "Histórico: Nobre", "Força ≥ 4", "Movimentação ≥ 6", "Atletismo ≥ 2");
    }

    [Fact]
    public void Descrever_omits_a_removed_pericia_and_flags_a_removed_historico()
    {
        var requisitos = new RequisitosDePassiva { HistoricoId = Guid.NewGuid(), CoracaoDeMana = false, Pericias = [new RequisitoDePericia(7, 2)] };

        PassivaRequisitosEvaluator.Descrever(requisitos, null, _ => null).Should().Equal("Histórico: (removido)");
    }
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter FullyQualifiedName~PassivaRequisitosEvaluatorTests`
Expected: build error — `Descrever` does not exist.

- [ ] **Step 3: Implement `Descrever`** — add to `PassivaRequisitosEvaluator` (after `Pendencias`):

```csharp
    /// <summary>
    /// Os requisitos por extenso, na ordem dos campos e com os mesmos rótulos de <see cref="Pendencias"/> —
    /// para o Livro de Regras, que não tem ficha para comparar. Uma Perícia sem nome (removida) é omitida.
    /// </summary>
    public static IReadOnlyList<string> Descrever(RequisitosDePassiva? requisitos, string? nomeDoHistoricoExigido, Func<int, string?> nomeDaPericia)
    {
        var itens = new List<string>();
        if (requisitos is null)
            return itens;

        if (requisitos.Nivel is { } nivel)
            itens.Add($"Nível {nivel}");
        if (requisitos.Vocacao is { } vocacao)
            itens.Add($"Vocação: {RequisitoLabels.Vocacao(vocacao)}");
        if (!string.IsNullOrWhiteSpace(requisitos.Classe))
            itens.Add($"Classe: {requisitos.Classe.Trim()}");
        if (requisitos.Linhagem is { } linhagem)
            itens.Add($"Linhagem: {RequisitoLabels.Linhagem(linhagem)}");
        if (requisitos.Variante is { } variante)
            itens.Add($"Variante: {RequisitoLabels.Variante(variante)}");
        if (requisitos.Graduacao is { } graduacao)
            itens.Add($"Grau/Círculo {graduacao}");
        if (requisitos.CoracaoDeMana == true)
            itens.Add("Coração de Mana");
        if (requisitos.Afinidade is { } afinidade)
            itens.Add($"Afinidade: {RequisitoLabels.Afinidade(afinidade)}");
        if (requisitos.Estrela is { } estrela)
            itens.Add($"Estrela: {estrela}");
        if (requisitos.HistoricoId is not null)
            itens.Add($"Histórico: {nomeDoHistoricoExigido ?? "(removido)"}");

        itens.AddRange(requisitos.Atributos.Select(r => $"{RequisitoLabels.Atributo(r.Atributo)} ≥ {r.Minimo}"));
        itens.AddRange(requisitos.SubAtributos.Select(r => $"{RequisitoLabels.SubAtributo(r.SubAtributo)} ≥ {r.Minimo}"));
        foreach (var r in requisitos.Pericias)
            if (nomeDaPericia(r.Pericia) is { } nome)
                itens.Add($"{nome} ≥ {r.Minimo}");

        return itens;
    }
```

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter FullyQualifiedName~PassivaRequisitosEvaluatorTests` → PASS.

- [ ] **Step 4: Create the contract** — `src/RuinaRPG.Contracts/Rules/PassivaDoLivroResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.Rules;

/// <summary>Uma Passiva na aba Habilidades Passivas do Livro de Regras. Categoria é o nome do enum; Requisitos já vem por extenso.</summary>
public record PassivaDoLivroResponse(string Id, string Nome, string Categoria, string Descricao, List<string> Requisitos);
```

- [ ] **Step 5: Write the failing integration tests** — create `tests/RuinaRPG.Tests.Integration/Controllers/RulebookPassivasControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Contracts.SpellsAndAbilities;

namespace RuinaRPG.Tests.Integration.Controllers;

/// <summary>Requisitos - Livro de Regras R0010: a aba Habilidades Passivas, por papel e por campanha.</summary>
public class RulebookPassivasControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public RulebookPassivasControllerTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_postgres.ConnectionString);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private static HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<string> RegisterGmAsync(string tag)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest($"LivroGm{tag}", $"livrogm{tag}@teste.com", "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private async Task<(string Id, string Token)> RegisterJogadorAsync(string gmToken, string tag)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest($"LivroPl{tag}", $"livropl{tag}@teste.com", "Senha!123", "Senha!123", code));
        var tokens = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        var me = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", tokens.AccessToken));
        return ((await me.Content.ReadFromJsonAsync<MeResponse>())!.Id, tokens.AccessToken);
    }

    private async Task<string> CreateCampaignAsync(string gmToken, string nome, string? memberId = null)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest(nome, "")));
        var id = (await response.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        if (memberId is not null)
            await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{id}/members", gmToken, new AddCampaignMemberRequest(memberId)));
        return id;
    }

    private async Task<string> CreateEntryAsync(string gmToken, string nome, string tipo, string? categoria, RequisitosDePassivaDto? requisitos = null)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", gmToken,
            new CreateSpellAbilityEntryRequest(nome, tipo, tipo == "Passiva" ? 0 : 1, "Descrição de " + nome, [], false, categoria, requisitos)));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SpellAbilityEntryResponse>())!.Id;
    }

    private async Task AttachAsync(string gmToken, string campaignId, string entryId, bool publica)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(null, null, null, entryId, null)));
        var attachmentId = (await response.Content.ReadFromJsonAsync<CampaignAttachmentResponse>())!.Id;
        if (publica)
            await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/attachments/{attachmentId}/visibility", gmToken, true));
    }

    private async Task<List<PassivaDoLivroResponse>> GetAsync(string token, string? campaignId = null)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook/passivas" + (campaignId is null ? "" : $"?campaignId={campaignId}"), token));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<PassivaDoLivroResponse>>())!;
    }

    [Fact]
    public async Task Anonymous_is_unauthorized_and_the_public_rulebook_still_answers()
    {
        (await _client.GetAsync("/api/rulebook/passivas")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _client.GetAsync("/api/rulebook/graus-e-circulos")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Gm_sees_every_passiva_of_their_own_bank_sorted_by_name_and_nothing_else()
    {
        var gm = await RegisterGmAsync("A");
        var outroGm = await RegisterGmAsync("A2");
        await CreateEntryAsync(gm, "Zelo", "Passiva", "DeClasse");
        await CreateEntryAsync(gm, "Ardor", "Passiva", "Livre", new RequisitosDePassivaDto(Nivel: 10, CoracaoDeMana: true));
        await CreateEntryAsync(gm, "Bola de Fogo", "Magia", null);
        await CreateEntryAsync(outroGm, "Do Outro", "Passiva", "Livre");

        var passivas = await GetAsync(gm);

        passivas.Select(p => p.Nome).Should().Equal("Ardor", "Zelo");
        passivas[0].Should().Match<PassivaDoLivroResponse>(p => p.Categoria == "Livre" && p.Descricao == "Descrição de Ardor");
        passivas[0].Requisitos.Should().Equal("Nível 10", "Coração de Mana");
        passivas[1].Should().Match<PassivaDoLivroResponse>(p => p.Categoria == "DeClasse" && p.Requisitos.Count == 0);
    }

    [Fact]
    public async Task Jogador_sees_only_the_passivas_public_in_the_chosen_campaign()
    {
        var gm = await RegisterGmAsync("B");
        var (playerId, player) = await RegisterJogadorAsync(gm, "B");
        var campanha = await CreateCampaignAsync(gm, "Campanha B", playerId);
        var outraCampanha = await CreateCampaignAsync(gm, "Campanha B2", playerId);
        var publica = await CreateEntryAsync(gm, "Pública", "Passiva", "Vocacional");
        var privada = await CreateEntryAsync(gm, "Privada", "Passiva", "Livre");
        var naoAnexada = await CreateEntryAsync(gm, "Não Anexada", "Passiva", "Livre");
        var magia = await CreateEntryAsync(gm, "Magia Pública", "Magia", null);
        var daOutra = await CreateEntryAsync(gm, "Da Outra", "Passiva", "Livre");
        await AttachAsync(gm, campanha, publica, publica: true);
        await AttachAsync(gm, campanha, privada, publica: false);
        await AttachAsync(gm, campanha, magia, publica: true);
        await AttachAsync(gm, outraCampanha, daOutra, publica: true);

        (await GetAsync(player, campanha)).Select(p => p.Nome).Should().Equal("Pública");
        (await GetAsync(player, outraCampanha)).Select(p => p.Nome).Should().Equal("Da Outra");
        naoAnexada.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Jogador_without_campaign_id_gets_400_unknown_campaign_404_and_non_member_403()
    {
        var gm = await RegisterGmAsync("C");
        var (_, player) = await RegisterJogadorAsync(gm, "C");
        var outroGm = await RegisterGmAsync("C2");
        var alheia = await CreateCampaignAsync(outroGm, "Campanha Alheia");
        await AttachAsync(outroGm, alheia, await CreateEntryAsync(outroGm, "Segredo", "Passiva", "Livre"), publica: true);

        (await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook/passivas", player))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/rulebook/passivas?campaignId={Guid.NewGuid()}", player))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/rulebook/passivas?campaignId={alheia}", player))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
```

If `CreateSpellAbilityEntryRequest`, `MeResponse`, `CampaignAttachmentResponse` or `AddCampaignMemberRequest` live in a namespace not imported above, add the `using` (see how `CampaignCatalogControllerTests.cs` and `NivelLimitsTests.cs` import them).

- [ ] **Step 6: Run to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~RulebookPassivasControllerTests`
Expected: FAIL — `/api/rulebook/passivas` is matched by `RulebookController.GetDocument("passivas")` and answers 404 (and 404 instead of 401 for anonymous).

- [ ] **Step 7: Implement the controller** — `src/RuinaRPG.Api/Controllers/RulebookPassivasController.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Requisitos - Livro de Regras R0010: a aba Habilidades Passivas. Ao contrário do resto do Livro
/// (RulebookController, anônimo e igual para todos), as Passivas vivem no banco de cada GM — por isso
/// este endpoint exige login e responde conforme o papel: o GM vê o próprio banco; o Jogador, as
/// Passivas anexadas como públicas à campanha escolhida (mesmo alcance de CampaignCatalogController).
/// </summary>
[ApiController]
[Authorize]
[Route("api/rulebook/passivas")]
public class RulebookPassivasController(RuinaRpgDbContext db, IPericiaCatalogo pericias) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PassivaDoLivroResponse>>> Get([FromQuery] Guid? campaignId)
    {
        var callerId = CurrentUserId();
        var query = db.SpellAbilityBankEntries.Where(e => e.Tipo == SpellAbilityTipo.Passiva && e.Categoria != null);

        if (User.IsInRole("GM"))
            query = query.Where(e => e.GmId == callerId);
        else
        {
            if (campaignId is not { } id)
                return BadRequest("Informe a campanha.");
            if (!await db.Campaigns.AnyAsync(c => c.Id == id))
                return NotFound();
            if (!await db.CampaignMembers.AnyAsync(m => m.CampaignId == id && m.UserId == callerId))
                return Forbid();
            query = query.Where(e => db.CampaignAttachments.Any(a => a.CampaignId == id && a.IsPublic && a.SpellAbilityBankEntryId == e.Id));
        }

        var entries = await query.OrderBy(e => e.Nome).ToListAsync();

        var historicoIds = entries.Select(e => e.Requisitos?.HistoricoId).OfType<Guid>().Distinct().ToList();
        var historicos = await db.Historicos.Where(h => historicoIds.Contains(h.Id)).ToDictionaryAsync(h => h.Id, h => h.Nome);
        var porId = await pericias.PorIdAsync();
        // Uma Perícia removida some dos requisitos, como em RequisitosDePassivaMapper.ToDto.
        string? NomeDaPericia(int periciaId) => porId.TryGetValue(periciaId, out var p) && !p.IsDeleted ? p.Nome : null;

        return entries.Select(e => new PassivaDoLivroResponse(
            e.Id.ToString(), e.Nome, e.Categoria!.Value.ToString(), e.Descricao,
            PassivaRequisitosEvaluator.Descrever(e.Requisitos,
                e.Requisitos?.HistoricoId is { } h ? historicos.GetValueOrDefault(h) : null, NomeDaPericia).ToList())).ToList();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
```

Adapt to the real shapes if they differ: `Historico`'s key/name properties (see `RequisitosDePassivaMapper.NomeDoHistoricoAsync`, which uses `h.Id`/`h.Nome`), and whether a soft-deleted Histórico is hidden by a query filter (if it is not, add the same filter `NomeDoHistoricoAsync` relies on). The literal route `api/rulebook/passivas` outranks `RulebookController`'s `api/rulebook/{slug}` in ASP.NET Core endpoint routing; the anonymous test in Step 5 proves it.

- [ ] **Step 8: Run tests + build**

Run: `dotnet build` → 0 warnings, 0 errors.
Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~RulebookPassivasControllerTests|FullyQualifiedName~RulebookControllerTests"`
Expected: PASS.

- [ ] **Step 9: Record the API change in the spec** — in the spec's `### API` subsection (section 2), replace the three bullets with:

```markdown
**Amended 2026-10-03 (planning):** one endpoint instead of two, returning ready-to-render rows.

- `GET /api/rulebook/passivas?campaignId={id}` (`RulebookPassivasController`, `[Authorize]`), returning
  `PassivaDoLivroResponse(Id, Nome, Categoria, Descricao, Requisitos)` with the Requisitos already written
  out by `PassivaRequisitosEvaluator.Descrever` (same labels as the sheet's pendências). The client would
  otherwise need the Histórico and Perícia catalogs just to print names.
- GM: their own bank's Passivas; `campaignId` ignored. Jogador: `campaignId` required (400), unknown
  campaign 404, non-member 403, otherwise the Passivas attached as public to it.
- The Jogador's campaign list comes from `GET /api/campaigns/mine`, which Minhas Campanhas already uses.
```

- [ ] **Step 10: Commit and push**

```bash
git add -A && git commit -m "feat: endpoint das Passivas do Livro de Regras, por papel e por campanha"   # + trailer lines
git push
```

---

### Task 5: Aba Habilidades Passivas no Livro de Regras (cliente)

**Files:**
- Create: `src/RuinaRPG.Client/Shared/PassivasDoLivro.razor`
- Modify: `src/RuinaRPG.Client/Pages/LivroDeRegras.razor`
- Create test: `tests/RuinaRPG.Tests.Client/Shared/PassivasDoLivroTests.cs`
- Test: `tests/RuinaRPG.Tests.Client/Pages/LivroDeRegrasTests.cs`

**Interfaces:**
- Consumes: `GET rulebook/passivas[?campaignId=]` → `List<PassivaDoLivroResponse>` (Task 4); `GET campaigns/mine` → `List<CampaignResponse>` (`Id`, `Nome`); `RequisitoLabels.Categoria(CategoriaDePassiva)`. The client's `HttpClient` base address already ends in `api/`, so URLs are relative without the `api/` prefix.
- Produces: component `PassivasDoLivro` with `[Parameter] public bool IsGm`.

- [ ] **Step 1: Write the failing component tests** — create `tests/RuinaRPG.Tests.Client/Shared/PassivasDoLivroTests.cs`:

```csharp
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.Rules;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class PassivasDoLivroTests : MudBunitContext
{
    private readonly List<string> _requests = new();

    private static PassivaDoLivroResponse Passiva(string nome, string categoria, params string[] requisitos) =>
        new(Guid.NewGuid().ToString(), nome, categoria, $"Desc {nome}", requisitos.ToList());

    private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    /// <summary>campaigns/mine devolve <paramref name="campanhas"/>; rulebook/passivas devolve o que <paramref name="passivas"/> der para a query string.</summary>
    private void Serve(List<CampaignResponse> campanhas, Func<string, List<PassivaDoLivroResponse>> passivas) =>
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            _requests.Add(request.RequestUri!.PathAndQuery);
            return request.RequestUri.AbsolutePath.EndsWith("campaigns/mine") ? Json(campanhas) : Json(passivas(request.RequestUri.Query));
        }));

    private static CampaignResponse Campanha(string id, string nome) => new(id, nome, "", null);

    [Fact]
    public async Task Gm_sees_the_bank_grouped_by_category_with_requisitos_and_no_campaign_selector()
    {
        Serve([], _ => [Passiva("Zelo", "DeClasse"), Passiva("Ardor", "Livre", "Nível 10", "Força ≥ 4"), Passiva("Brio", "Vocacional")]);

        var cut = Render<PassivasDoLivro>(p => p.Add(x => x.IsGm, true));
        await Task.Delay(50);

        _requests.Should().Equal("/api/rulebook/passivas");
        cut.FindComponents<MudSelect<string>>().Should().BeEmpty();
        var texto = cut.Markup;
        texto.IndexOf("Passiva Livre").Should().BeLessThan(texto.IndexOf("Passiva Vocacional"));
        texto.IndexOf("Passiva Vocacional").Should().BeLessThan(texto.IndexOf("Passiva de Classe"));
        texto.Should().Contain("Ardor").And.Contain("Desc Ardor").And.Contain("Nível 10, Força ≥ 4");
        texto.Should().Contain("Sem requisitos");
    }

    [Fact]
    public async Task Jogador_with_one_campaign_loads_it_directly_without_a_selector()
    {
        Serve([Campanha("c1", "Campanha Um")], _ => [Passiva("Ardor", "Livre")]);

        var cut = Render<PassivasDoLivro>(p => p.Add(x => x.IsGm, false));
        await Task.Delay(50);

        _requests.Should().Equal("/api/campaigns/mine", "/api/rulebook/passivas?campaignId=c1");
        cut.FindComponents<MudSelect<string>>().Should().BeEmpty();
        cut.Markup.Should().Contain("Ardor");
    }

    [Fact]
    public async Task Jogador_with_several_campaigns_gets_a_selector_that_switches_the_list()
    {
        Serve([Campanha("c1", "Campanha Um"), Campanha("c2", "Campanha Dois")],
            query => query.Contains("c2") ? [Passiva("Da Dois", "Livre")] : [Passiva("Da Um", "Livre")]);

        var root = RenderWithPopover<PassivasDoLivro>((nameof(PassivasDoLivro.IsGm), false));
        await Task.Delay(50);

        root.Markup.Should().Contain("Da Um").And.NotContain("Da Dois");
        OpenSelect(root, "Campanha").Should().Equal("Campanha Um", "Campanha Dois");
        root.FindAll(".mud-list-item").Single(li => li.TextContent.Trim() == "Campanha Dois").Click();
        await Task.Delay(50);

        root.Markup.Should().Contain("Da Dois").And.NotContain("Da Um");
        _requests.Should().Contain("/api/rulebook/passivas?campaignId=c2");
    }

    [Fact]
    public async Task Jogador_without_campaigns_sees_the_empty_state_and_requests_no_passivas()
    {
        Serve([], _ => []);

        var cut = Render<PassivasDoLivro>(p => p.Add(x => x.IsGm, false));
        await Task.Delay(50);

        cut.Markup.Should().Contain("Você ainda não participa de nenhuma campanha.");
        _requests.Should().Equal("/api/campaigns/mine");
    }

    [Fact]
    public async Task Empty_list_shows_the_empty_state()
    {
        Serve([], _ => []);

        var cut = Render<PassivasDoLivro>(p => p.Add(x => x.IsGm, true));
        await Task.Delay(50);

        cut.Markup.Should().Contain("Nenhuma Habilidade Passiva disponível.");
    }

    [Fact]
    public async Task Search_filters_by_name_ignoring_case_and_hides_empty_groups()
    {
        Serve([], _ => [Passiva("Ardor", "Livre"), Passiva("Pele de Pedra", "Vocacional")]);

        var cut = Render<PassivasDoLivro>(p => p.Add(x => x.IsGm, true));
        await Task.Delay(50);
        cut.FindComponent<MudTextField<string>>().Find("input").Input("PELE");

        cut.Markup.Should().Contain("Pele de Pedra").And.NotContain("Ardor");
        cut.Markup.Should().NotContain("Passiva Livre");
    }

    [Fact]
    public async Task A_failed_load_shows_an_error_message()
    {
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var cut = Render<PassivasDoLivro>(p => p.Add(x => x.IsGm, true));
        await Task.Delay(50);

        cut.Markup.Should().Contain("Não foi possível carregar as Habilidades Passivas.");
    }
}
```

The MudBlazor interaction mechanics (opening a select, typing into a text field) follow `MudBunitContext.OpenSelect` and the existing component tests; if a selector above does not match MudBlazor's rendered DOM, adapt the *interaction* to the pattern another test in this project already uses, keeping the assertions.

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~PassivasDoLivroTests`
Expected: build error — `PassivasDoLivro` does not exist.

- [ ] **Step 3: Implement the component** — `src/RuinaRPG.Client/Shared/PassivasDoLivro.razor`:

```razor
@inject HttpClient Http
@using System.Net.Http.Json
@using MudBlazor
@using RuinaRPG.Contracts.Campaigns
@using RuinaRPG.Contracts.Rules
@using RuinaRPG.Domain.SpellsAndAbilities

@* Livro de Regras R0010: as Passivas ao alcance de quem está logado — o GM vê o próprio banco; o
   Jogador, as públicas da campanha escolhida (seletor só com mais de uma campanha). *@
<DismissibleAlert @bind-Message="_errorMessage" Class="mb-3" />

@if (!IsGm && _campanhas.Count > 1)
{
    <MudSelect T="string" Value="_campanhaId" ValueChanged="OnCampanhaChangedAsync" Label="Campanha" Class="mb-4">
        @foreach (var campanha in _campanhas)
        {
            <MudSelectItem Value="@campanha.Id">@campanha.Nome</MudSelectItem>
        }
    </MudSelect>
}

@if (_carregado)
{
    @if (!IsGm && _campanhas.Count == 0)
    {
        <MudText>Você ainda não participa de nenhuma campanha.</MudText>
    }
    else if (_passivas.Count == 0)
    {
        <MudText>Nenhuma Habilidade Passiva disponível.</MudText>
    }
    else
    {
        <MudTextField T="string" @bind-Value="_filtro" Label="Buscar passiva..." Immediate="true" Class="mb-4" />
        @foreach (var categoria in Enum.GetValues<CategoriaDePassiva>())
        {
            var doGrupo = _passivas
                .Where(p => p.Categoria == categoria.ToString()
                    && (string.IsNullOrWhiteSpace(_filtro) || p.Nome.Contains(_filtro, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            @if (doGrupo.Count > 0)
            {
                <MudText Typo="Typo.h5" Class="mt-4">@RequisitoLabels.Categoria(categoria)</MudText>
                @foreach (var passiva in doGrupo)
                {
                    <Section Title="@passiva.Nome">
                        <MudText Style="white-space: pre-wrap">@passiva.Descricao</MudText>
                        <MudText Typo="Typo.body2" Class="mt-2">
                            <b>Requisitos:</b> @(passiva.Requisitos.Count > 0 ? string.Join(", ", passiva.Requisitos) : "Sem requisitos")
                        </MudText>
                    </Section>
                }
            }
        }
    }
}

@code {
    [Parameter] public bool IsGm { get; set; }

    private List<CampaignResponse> _campanhas = new();
    private List<PassivaDoLivroResponse> _passivas = new();
    private string? _campanhaId;
    private string _filtro = "";
    private string? _errorMessage;
    private bool _carregado;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            if (!IsGm)
            {
                _campanhas = await Http.GetFromJsonAsync<List<CampaignResponse>>("campaigns/mine") ?? new();
                _campanhaId = _campanhas.FirstOrDefault()?.Id;
            }
            await CarregarPassivasAsync();
        }
        catch (Exception)
        {
            _errorMessage = "Não foi possível carregar as Habilidades Passivas.";
        }
        _carregado = true;
    }

    private async Task OnCampanhaChangedAsync(string? campanhaId)
    {
        _campanhaId = campanhaId;
        try
        {
            await CarregarPassivasAsync();
        }
        catch (Exception)
        {
            _errorMessage = "Não foi possível carregar as Habilidades Passivas.";
        }
    }

    private async Task CarregarPassivasAsync()
    {
        // Jogador sem campanha: não há o que pedir (o endpoint exige campaignId).
        if (!IsGm && _campanhaId is null)
        {
            _passivas = new();
            return;
        }
        var url = IsGm ? "rulebook/passivas" : $"rulebook/passivas?campaignId={_campanhaId}";
        _passivas = await Http.GetFromJsonAsync<List<PassivaDoLivroResponse>>(url) ?? new();
    }
}
```

If the `Section` component requires `ChildContent` to be explicit when other fragments exist, it does not here (only child content is passed). In the failed-load case `_passivas` is empty, so "Nenhuma Habilidade Passiva disponível." would also render under the alert — guard it: show the empty states only when `_errorMessage is null` (change `@if (_carregado)` to `@if (_carregado && _errorMessage is null)`).

- [ ] **Step 4: Run the component tests**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~PassivasDoLivroTests`
Expected: PASS.

- [ ] **Step 5: Write the failing page tests** — append to `LivroDeRegrasTests`:

```csharp
    private void ServeRulebook() =>
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(request.RequestUri!.AbsolutePath.EndsWith("/rulebook")
                ? """
                  [{"slug":"sistema-basico","titulo":"Sistema Básico","introHtml":"<p>a</p>","sections":[]},
                   {"slug":"graus-e-circulos","titulo":"Graus & Círculos","introHtml":"<p>b</p>","sections":[]},
                   {"slug":"tabela-de-niveis","titulo":"Tabela de Níveis","introHtml":"<p>c</p>","sections":[]}]
                  """
                : "[]", Encoding.UTF8, "application/json"),
        }));

    private List<string> TabTitles(IRenderedComponent<CascadingAuthenticationState> cut) =>
        cut.FindAll(".mud-tab").Select(t => t.TextContent.Trim()).ToList();

    [Fact]
    public async Task Logged_in_user_gets_the_Habilidades_Passivas_tab_right_after_Graus_e_Circulos()
    {
        ServeRulebook();
        var auth = AddAuthorization();
        auth.SetAuthorized("Teste");
        auth.SetRoles("Jogador");

        var cut = Render<CascadingAuthenticationState>(p => p.AddChildContent<LivroDeRegras>());
        await Task.Delay(50);

        TabTitles(cut).Should().Equal("Sistema Básico", "Graus & Círculos", "Habilidades Passivas", "Tabela de Níveis");
    }

    [Fact]
    public async Task Anonymous_visitor_does_not_get_the_Habilidades_Passivas_tab()
    {
        ServeRulebook();
        AddAuthorization().SetNotAuthorized();

        var cut = Render<CascadingAuthenticationState>(p => p.AddChildContent<LivroDeRegras>());
        await Task.Delay(50);

        TabTitles(cut).Should().Equal("Sistema Básico", "Graus & Círculos", "Tabela de Níveis");
    }
```

- [ ] **Step 6: Run to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~LivroDeRegrasTests`
Expected: the logged-in test FAILS (no such tab); the anonymous one passes.

- [ ] **Step 7: Implement in `LivroDeRegras.razor`**

Right after the `</MudTabPanel>` that closes each document's panel (still inside the `@foreach`), add:

```razor
            @* R0010: a aba de Passivas não vem do GET rulebook (anônimo) — só existe para quem está logado. *@
            @if (_autenticado && document.Slug == "graus-e-circulos")
            {
                <MudTabPanel Text="Habilidades Passivas">
                    <PassivasDoLivro IsGm="@_ehGm" />
                </MudTabPanel>
            }
```

In `@code`, add the fields and set them in `OnInitializedAsync` (reusing the `authState` already read there):

```csharp
    private bool _autenticado;
    private bool _ehGm;
```

```csharp
        var authState = AuthState is null ? null : await AuthState;
        _autenticado = authState?.User.Identity?.IsAuthenticated == true;
        _ehGm = authState?.User.IsInRole("GM") == true;
        if (!_autenticado)
        {
            Crumbs = new()
            {
                new("Início", ""),
                new("Livro de Regras"),
            };
        }
```

- [ ] **Step 8: Run the client suite and build**

Run: `dotnet test tests/RuinaRPG.Tests.Client` → PASS.
Run: `dotnet build` → 0 warnings, 0 errors.

- [ ] **Step 9: Commit and push**

```bash
git add -A && git commit -m "feat(client): aba Habilidades Passivas no Livro de Regras"   # + trailer lines
git push
```

---

### Task 6: Requisitos, versão 1.4.2 e changelog

**Files:**
- Modify: `Docs/Requisitos/Requisitos - Auditoria de Regras.md` (R0013)
- Modify: `Docs/Requisitos/Requisitos - Ficha de Personagem.md` (the "Painel Progressão do nível" note near line 95; 4.f near line 338)
- Modify: `Docs/Requisitos/Requisitos - Livro de Regras.md` (access-model preamble; new R0010)
- Modify: `Docs/Requisitos/Requisitos - Modelo de Dados.md` (only if it lists the system column keys — `grep -n "MaxPassivas" "Docs/Requisitos/Requisitos - Modelo de Dados.md"`)
- Modify: `src/RuinaRPG.Domain/AppVersionInfo.cs`
- Modify: `src/RuinaRPG.Client/Shared/ChangelogDialog.razor`
- Test: `tests/RuinaRPG.Tests.Client/Shared/ChangelogDialogTests.cs`

**Interfaces:**
- Consumes: the behaviour of Tasks 1-5. Read the spec before writing — the Requisitos must say what the spec says, in Brazilian Portuguese, in the style of the surrounding text (wikilinks `[[...]]`, no numbers duplicated, no emojis).

- [ ] **Step 1: Write the failing changelog test** — in `ChangelogDialogTests`, replace `Rendered_list_contains_the_section_labels_of_1_4_1_and_does_not_contain_Auditoria` with:

```csharp
    [Fact]
    public void Rendered_list_contains_the_section_labels_of_1_4_2_and_does_not_contain_Auditoria()
    {
        var cut = RenderDialog("1.4.2", EventCallback.Factory.Create(this, () => { }));

        cut.Markup.Should().Contain("Habilidade Passiva de qualquer categoria");
        cut.Markup.Should().Contain("Livro de Regras");
        cut.Markup.Should().Contain("Habilidades Passivas");
        cut.Markup.Should().NotContain("Evoluções de Arca"); // item da 1.4.1 — a lista é só da versão atual
        cut.Markup.Should().NotContain("Auditoria");
    }
```

And in `Renders_the_Version_parameter_in_the_title_not_a_hardcoded_literal`, change `NotContain("1.4.1")` to `NotContain("1.4.2")`.

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~ChangelogDialogTests` → the new test FAILS.

- [ ] **Step 2: Bump the version and rewrite the changelog**

`AppVersionInfo.cs`: `public const string Current = "1.4.2";`

`ChangelogDialog.razor`: change the comment to "Texto da versão 1.4.2" and replace the whole `<ul>` content with:

```razor
            <li><b>Habilidade Passiva de qualquer categoria:</b> a Tabela de Níveis pode conceder vagas de Habilidade Passiva que aceitam uma Passiva Livre, Vocacional ou de Classe, além das vagas de cada categoria. As fichas de Personagem e NPC passam a aceitar a Passiva quando a categoria está cheia mas ainda há uma dessas vagas.</li>
            <li><b>Progressão do nível e aviso de subida de nível:</b> as vagas de Passiva aparecem como "Habilidade Passiva", "Passiva Livre", "Passiva Vocacional" e "Passiva de Classe".</li>
            <li><b>Livro de Regras:</b> nova aba Habilidades Passivas, depois de Graus &amp; Círculos, para quem está logado. O GM vê as Passivas do próprio banco; o jogador, as que o GM tornou públicas na campanha, com um seletor quando participa de mais de uma. A aba tem busca por nome e mostra os requisitos de cada Passiva.</li>
```

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~ChangelogDialogTests` → PASS. Then `grep -rn "1\.4\.1" src tests --include=*.cs --include=*.razor` and update any remaining assertion that pins the current version (leave historical mentions in comments alone).

- [ ] **Step 3: Update `Requisitos - Auditoria de Regras.md` R0013**

- In the **Acumulativa** bullet, after the sentence about the three Passiva columns, add: a quarta coluna, **Passivas Coringa**, também Acumulativa, concede vagas que aceitam uma Passiva de **qualquer** categoria; vazia vale 0 vagas (não "sem limite").
- In the **Limites** paragraph, describe the rule: o que passar do limite de uma categoria ocupa vagas coringa; a adição é recusada quando a categoria está cheia e todas as vagas coringa já estão em uso; categoria com coluna inteiramente vazia segue sem limite e não ocupa coringa; nada é marcado na Passiva.
- Add a paragraph on naming: fora das páginas de Auditoria as quatro colunas de Passiva têm rótulo fixo — "Habilidade Passiva" (Coringa), "Passiva Livre", "Passiva Vocacional", "Passiva de Classe" — no painel Progressão do nível, no aviso de subida de nível e na aba Tabela de Níveis do Livro de Regras; renomear uma dessas colunas só muda a grade da Auditoria. Update the level-up example ("Passivas Livres: +1" → "Passiva Livre: +1").
- Note that a system column added by a new version enters right after the previous system column in tables already seeded.

- [ ] **Step 4: Update `Requisitos - Ficha de Personagem.md`**

- "Painel Progressão do nível" note: the Passiva rows use the fixed labels; the three category rows show the accumulated limit or "sem limite"; "Habilidade Passiva" shows the accumulated wildcard slots (0 when the column is empty).
- 4.f limit paragraph (near "A ficha não pode ter mais Passivas de uma Categoria..."): add that a Passiva beyond the category's limit is accepted while there is a free vaga coringa (coluna Passivas Coringa; R0013 da Auditoria), and that the sheet does not mark which Passiva occupies it.

- [ ] **Step 5: Update `Requisitos - Livro de Regras.md`**

- Access-model preamble: add that the aba Habilidades Passivas (R0010) is the exception — only for authenticated users.
- R0001: mention that, for logged-in users, an eighth tab (Habilidades Passivas, R0010) sits right after Graus & Círculos.
- Append **R0010** at the end, following the heading format `# **R0010** - ...` and a `**Descrição**:` paragraph, covering: logged-in only; GM sees every Passiva of their own bank; Jogador sees the Passivas attached as public to the chosen campaign (Campanha R0008/R0009), with a campaign selector only when they are in more than one, and an empty-state message with none; layout like R0005 (search by name, grouped by Passiva Livre / Passiva Vocacional / Passiva de Classe, one card per Passiva with description and Requisitos por extenso or "Sem requisitos"); content comes live from "[[Requisitos - Banco de Magias e Habilidades]]" R0009, not from Markdown, and is read-only here (R0003).

- [ ] **Step 6: Full verification**

Run: `dotnet build` → 0 warnings, 0 errors.
Run: `dotnet test` → all pass (rerun in isolation any integration test that times out; see Global Constraints).

- [ ] **Step 7: Commit and push**

```bash
git add -A && git commit -m "docs: requisitos da Passiva Coringa e da aba Habilidades Passivas; versão 1.4.2"   # + trailer lines
git push
```
