# Tabela de Afinidades e versão 1.4.4 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eficiência Elemental e Dano Elemental passam a ser consultados numa tabela editável pelo Auditor de Regras (CRUD), e a versão vai para 1.4.4 com o changelog acumulado desde a 1.4.3.

**Architecture:** Função pura no Domain recebe o valor da afinidade e as linhas da tabela. As linhas vivem numa tabela Postgres (`TabelaDeAfinidades`), semeada do Markdown embutido só enquanto vazia, lida por um provider scoped e editada por um controller gated ao Auditor. Mesmo molde da Durabilidade por Rank — os arquivos dela são o modelo de estilo para cada camada.

**Tech Stack:** .NET 8, ASP.NET Core, EF Core/Npgsql, Blazor WASM + MudBlazor, xUnit + FluentAssertions, bUnit, Testcontainers.

**Spec:** `docs/superpowers/specs/2026-10-09-tabela-de-afinidades-design.md` — leia antes de começar qualquer task.

## Global Constraints

- Branch `release/1.4.4`. Um commit por task, mensagem em português no estilo do `git log` (`feat:`, `feat(client):`, `docs:`…), terminando com as linhas de atribuição que o controlador informar.
- TDD obrigatório: teste falhando primeiro, depois o mínimo de código.
- `dotnet build` termina com **0 warnings, 0 errors** ao fim de cada task.
- Todo texto visível ao usuário e toda a documentação em `Docs/` em português do Brasil. Ícones MudBlazor, nunca emojis.
- `Docs/` usa `[[wikilinks]]`; números da tabela não são repetidos nos requisitos.
- Testes de integração precisam do Docker; rode sempre com filtro (`--filter FullyQualifiedName~X`). Há um flake conhecido de timeout: um teste que falhar por timeout deve ser rodado de novo sozinho antes de ser tratado como falha real.
- Regra de consulta (copiada da spec): vale a linha de maior Afinidade que não ultrapassa o valor; abaixo da menor linha ou tabela vazia → 0 e 0; a ordem das linhas na entrada não importa.
- Validação da API: Afinidade, Eficiência e Dano inteiros ≥ 0 (400); Afinidade repetida → 409; id inexistente → 404; não-Auditor → 403.

## Review Focus

1. Tabela com lacunas (ex.: linhas 0, 5, 10 e valor 7) → vale a linha 5. Teste na Task 1.
2. Valor da afinidade negativo ou abaixo da menor linha (tabela começando em 3, valor 1) → 0 e 0, sem exceção. Teste na Task 1.
3. Auditor exclui linhas e a API reinicia → o seeder não ressuscita linhas enquanto restar alguma. Teste na Task 2.
4. PUT que mantém a própria Afinidade da linha não pode dar 409 contra si mesma; PUT para a Afinidade de outra linha dá 409. Teste na Task 3.
5. Editar uma linha pela Auditoria reflete na ficha na requisição seguinte (provider scoped, sem cache entre requisições). Teste na Task 3.

---

### Task 1: Domain — consulta na tabela, parser e arquivo-fonte

**Files:**
- Rename: `Docs/Sistema RPG/Tabela_Afinidades.md` → `Docs/Sistema RPG/Tabela de Afinidades.md` (o arquivo está não rastreado: use `mv`, depois `git add`)
- Create: `src/RuinaRPG.Domain/CharacterSheets/LinhaDaTabelaDeAfinidades.cs`
- Create: `src/RuinaRPG.Domain/Rules/TabelaDeAfinidadesParser.cs`
- Modify: `src/RuinaRPG.Domain/CharacterSheets/SubAttributeFormulas.cs`
- Modify: `src/RuinaRPG.Infrastructure/RuinaRPG.Infrastructure.csproj` (novo `EmbeddedResource`)
- Test: `tests/RuinaRPG.Tests.Unit/CharacterSheets/SubAttributeFormulasTests.cs`, `tests/RuinaRPG.Tests.Unit/Rules/TabelaDeAfinidadesParserTests.cs`

**Interfaces:**
- Produces:
  - `public readonly record struct LinhaDaTabelaDeAfinidades(int Afinidade, int Eficiencia, int Dano);` (namespace `RuinaRPG.Domain.CharacterSheets`)
  - `SubAttributeFormulas.EficienciaElemental(int valorDaAfinidadeCorrespondente, IReadOnlyList<LinhaDaTabelaDeAfinidades> tabela) : int`
  - `SubAttributeFormulas.DanoElemental(int valorDaAfinidadeCorrespondente, IReadOnlyList<LinhaDaTabelaDeAfinidades> tabela) : int`
  - `TabelaDeAfinidadesParser.Parse(string markdown) : IReadOnlyList<LinhaDaTabelaDeAfinidades>` (namespace `RuinaRPG.Domain.Rules`)
  - Recurso embutido `"Tabela de Afinidades.md"`, lido com `RulesDataProvider.ReadResource("Tabela de Afinidades.md")`.
- Nesta task as sobrecargas antigas de um argumento e as constantes `PontosPor*` **continuam existindo** (a Task 3 as remove) — o build precisa ficar verde.

- [ ] **Step 1: Renomear o arquivo e dar-lhe a introdução**

```bash
mv "Docs/Sistema RPG/Tabela_Afinidades.md" "Docs/Sistema RPG/Tabela de Afinidades.md"
```

Insira antes da tabela, seguido de uma linha em branco (as 22 linhas da tabela ficam intactas):

```markdown
Eficiência Elemental e Dano Elemental conforme o valor da Essência Básica ou do Elemento da Afinidade escolhida na ficha. Vale a linha de maior Afinidade que não ultrapassa o valor; acima da última linha, valem a Eficiência e o Dano dela. Os valores podem ser ajustados pelo Auditor de Regras (ver [[Requisitos - Auditoria de Regras]]).
```

Adicione ao `.csproj` da Infrastructure, logo após a linha da Durabilidade:

```xml
    <EmbeddedResource Include="../../Docs/Sistema RPG/Tabela de Afinidades.md" LogicalName="Tabela de Afinidades.md" />
```

- [ ] **Step 2: Testes falhando da consulta** — acrescente a `SubAttributeFormulasTests.cs` (mantenha o teste antigo de ÷2/÷3 por enquanto):

```csharp
    private static readonly LinhaDaTabelaDeAfinidades[] TabelaComLacunas =
    {
        new(10, 5, 5), new(3, 2, 1), new(5, 3, 2), // fora de ordem de propósito
    };

    [Theory]
    [InlineData(3, 2, 1)]   // valor exato
    [InlineData(4, 2, 1)]   // entre linhas: vale a maior que não ultrapassa
    [InlineData(7, 3, 2)]   // lacuna entre 5 e 10
    [InlineData(10, 5, 5)]  // última linha
    [InlineData(99, 5, 5)]  // acima da tabela: valores máximos
    [InlineData(2, 0, 0)]   // abaixo da menor linha
    [InlineData(-1, 0, 0)]  // negativo
    public void EficienciaElemental_and_DanoElemental_come_from_the_highest_row_not_above_the_value(int valor, int eficiencia, int dano)
    {
        SubAttributeFormulas.EficienciaElemental(valor, TabelaComLacunas).Should().Be(eficiencia);
        SubAttributeFormulas.DanoElemental(valor, TabelaComLacunas).Should().Be(dano);
    }

    [Fact]
    public void EficienciaElemental_and_DanoElemental_are_0_with_an_empty_table()
    {
        SubAttributeFormulas.EficienciaElemental(7, Array.Empty<LinhaDaTabelaDeAfinidades>()).Should().Be(0);
        SubAttributeFormulas.DanoElemental(7, Array.Empty<LinhaDaTabelaDeAfinidades>()).Should().Be(0);
    }
```

- [ ] **Step 3: Rodar e ver falhar** — `dotnet test tests/RuinaRPG.Tests.Unit --filter FullyQualifiedName~SubAttributeFormulasTests` → falha de compilação (tipo e sobrecargas não existem).

- [ ] **Step 4: Implementar**

`LinhaDaTabelaDeAfinidades.cs`:

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Uma linha da Tabela de Afinidades: a partir de <paramref name="Afinidade"/> pontos na essência ou
/// elemento da Afinidade escolhida, valem esta Eficiência Elemental e este Dano Elemental.
/// </summary>
public readonly record struct LinhaDaTabelaDeAfinidades(int Afinidade, int Eficiencia, int Dano);
```

Em `SubAttributeFormulas.cs`, ao lado das sobrecargas atuais:

```csharp
    // 2.b: Eficiência e Dano Elemental vêm da Tabela de Afinidades — vale a linha de maior Afinidade que
    // não ultrapassa o valor; abaixo da menor linha (ou sem linhas) os dois valem 0.
    public static int EficienciaElemental(int valorDaAfinidadeCorrespondente, IReadOnlyList<LinhaDaTabelaDeAfinidades> tabela) =>
        LinhaDaTabela(valorDaAfinidadeCorrespondente, tabela).Eficiencia;

    public static int DanoElemental(int valorDaAfinidadeCorrespondente, IReadOnlyList<LinhaDaTabelaDeAfinidades> tabela) =>
        LinhaDaTabela(valorDaAfinidadeCorrespondente, tabela).Dano;

    private static LinhaDaTabelaDeAfinidades LinhaDaTabela(int valor, IReadOnlyList<LinhaDaTabelaDeAfinidades> tabela) =>
        tabela.Where(l => l.Afinidade <= valor).OrderByDescending(l => l.Afinidade).FirstOrDefault();
```

(`FirstOrDefault` de struct devolve `default` = 0/0/0 quando nada casa.)

- [ ] **Step 5: Testes falhando do parser** — `TabelaDeAfinidadesParserTests.cs` (use `CirculoGrauPorEapParserTests.cs` como modelo de como os testes unitários leem um arquivo real de `Docs/Sistema RPG/`, se algum o fizer; senão cole o Markdown inline):

```csharp
    [Fact]
    public void Parse_reads_every_row_of_the_table()
    {
        const string markdown = "Texto de introdução.\n\n| Afinidade | Eficiência | Dano |\n| --------- | ---------- | ---- |\n| 0         | 0          | 0    |\n| 1         | 1          | 0    |\n| 21        | 11         | 10   |\n";

        TabelaDeAfinidadesParser.Parse(markdown).Should().Equal(
            new LinhaDaTabelaDeAfinidades(0, 0, 0), new LinhaDaTabelaDeAfinidades(1, 1, 0), new LinhaDaTabelaDeAfinidades(21, 11, 10));
    }

    [Theory]
    [InlineData("| x | 1 | 1 |")]
    [InlineData("| 1 | -1 | 1 |")]
    [InlineData("| 1 | 1 |")]
    public void Parse_rejects_a_malformed_row(string linha)
    {
        var act = () => TabelaDeAfinidadesParser.Parse("| Afinidade | Eficiência | Dano |\n| --- | --- | --- |\n" + linha);
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Parse_rejects_a_repeated_Afinidade()
    {
        var act = () => TabelaDeAfinidadesParser.Parse("| Afinidade | Eficiência | Dano |\n| --- | --- | --- |\n| 1 | 1 | 0 |\n| 1 | 2 | 1 |");
        act.Should().Throw<FormatException>();
    }
```

- [ ] **Step 6: Implementar o parser** no estilo de `src/RuinaRPG.Domain/Rules/TabelaDeDurabilidadeParser.cs`: percorre as linhas que começam com `|`, pula o cabeçalho (`cells[0] == "Afinidade"`) e o separador (`cells[0].StartsWith('-')`), exige exatamente 3 células inteiras ≥ 0 e Afinidade não repetida; senão `FormatException` com mensagem em português.

- [ ] **Step 7: Teste do arquivo real** — acrescente a `tests/RuinaRPG.Tests.Unit` ou, se o projeto Unit não referenciar a Infrastructure, a `tests/RuinaRPG.Tests.Integration` (um teste sem banco), um Fact que faz `TabelaDeAfinidadesParser.Parse(RulesDataProvider.ReadResource("Tabela de Afinidades.md"))` e espera 22 linhas, a primeira `(0,0,0)` e a última `(21,11,10)`.

- [ ] **Step 8: Rodar** — `dotnet test tests/RuinaRPG.Tests.Unit` verde; `dotnet build` com 0 warnings.

- [ ] **Step 9: Commit** — `feat: consulta de Eficiência e Dano Elemental na Tabela de Afinidades (Domain) e arquivo-fonte`

---

### Task 2: Infrastructure — tabela, migration, seeder e provider

**Files:**
- Create: `src/RuinaRPG.Infrastructure/CharacterSheets/AfinidadeElementalLinha.cs`
- Create: `src/RuinaRPG.Infrastructure/CharacterSheets/TabelaDeAfinidadesSeeder.cs`
- Create: `src/RuinaRPG.Infrastructure/CharacterSheets/TabelaDeAfinidadesProvider.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Create: migration `AddTabelaDeAfinidades` (comando no CLAUDE.md)
- Modify: `src/RuinaRPG.Api/Program.cs` (registro do provider; seeder nos dois pontos onde `DurabilidadePorRankSeeder.SeedAsync` é chamado — linhas ~237 e ~320)
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/TabelaDeAfinidadesSeederTests.cs`

**Interfaces:**
- Consumes: `LinhaDaTabelaDeAfinidades`, `TabelaDeAfinidadesParser.Parse` (Task 1).
- Produces:
  - `class AfinidadeElementalLinha { Guid Id; int Afinidade; int Eficiencia; int Dano; }`
  - `RuinaRpgDbContext.TabelaDeAfinidades : DbSet<AfinidadeElementalLinha>` (tabela `TabelaDeAfinidades`, índice único em `Afinidade`)
  - `TabelaDeAfinidadesSeeder.SeedAsync(RuinaRpgDbContext db, string markdown) : Task<int>`
  - `TabelaDeAfinidadesProvider(RuinaRpgDbContext db)` com `Task<IReadOnlyList<LinhaDaTabelaDeAfinidades>> LinhasAsync()`, registrado como scoped.

- [ ] **Step 1: Testes falhando do seeder** — modelo de fixture: `tests/RuinaRPG.Tests.Integration/Persistence/DurabilidadePorRankMigrationTests.cs` e os demais testes de seeder do projeto (procure `SeederTests`). Cada teste começa limpando a tabela (`db.TabelaDeAfinidades.ExecuteDeleteAsync()`), porque o banco é compartilhado e o startup da API pode já tê-la semeado; ao fim, restaure semeando do arquivo real.
  - `Seed_inserts_every_row_when_the_table_is_empty`: tabela vazia + Markdown de 3 linhas → devolve 3 e as 3 linhas existem.
  - `Seed_inserts_nothing_when_the_table_already_has_rows` (Review Focus 3): tabela com uma única linha `(4, 9, 9)` + o mesmo Markdown → devolve 0 e a tabela continua só com essa linha.

- [ ] **Step 2: Rodar e ver falhar** (compilação).

- [ ] **Step 3: Implementar**

```csharp
namespace RuinaRPG.Infrastructure.CharacterSheets;

/// <summary>
/// Uma linha da Tabela de Afinidades — semeada de "Tabela de Afinidades.md" e mantida (criar, editar,
/// excluir) pelo Auditor de Regras. Afinidade é única.
/// </summary>
public class AfinidadeElementalLinha
{
    public Guid Id { get; set; }
    public int Afinidade { get; set; }
    public int Eficiencia { get; set; }
    public int Dano { get; set; }
}
```

DbContext: `public DbSet<AfinidadeElementalLinha> TabelaDeAfinidades => Set<AfinidadeElementalLinha>();` e, em `OnModelCreating`, `builder.Entity<AfinidadeElementalLinha>().ToTable("TabelaDeAfinidades").HasIndex(l => l.Afinidade).IsUnique();` (siga a forma como as entidades vizinhas são configuradas).

```csharp
/// <summary>
/// Semeia a Tabela de Afinidades só enquanto ela está vazia. Não insere "as linhas que faltam", como a
/// Durabilidade por Rank: aqui o Auditor pode excluir linhas, e isso as ressuscitaria.
/// </summary>
public static class TabelaDeAfinidadesSeeder
{
    public static async Task<int> SeedAsync(RuinaRpgDbContext db, string markdown)
    {
        if (await db.TabelaDeAfinidades.AnyAsync())
            return 0;

        var linhas = TabelaDeAfinidadesParser.Parse(markdown);
        foreach (var linha in linhas)
            db.TabelaDeAfinidades.Add(new AfinidadeElementalLinha { Id = Guid.NewGuid(), Afinidade = linha.Afinidade, Eficiencia = linha.Eficiencia, Dano = linha.Dano });
        await db.SaveChangesAsync();
        return linhas.Count;
    }
}
```

```csharp
/// <summary>Tabela de Afinidades carregada uma vez por escopo, para os cálculos de Sub-Atributos.</summary>
public class TabelaDeAfinidadesProvider(RuinaRpgDbContext db)
{
    private IReadOnlyList<LinhaDaTabelaDeAfinidades>? _linhas;

    public async Task<IReadOnlyList<LinhaDaTabelaDeAfinidades>> LinhasAsync() =>
        _linhas ??= await db.TabelaDeAfinidades.AsNoTracking()
            .Select(l => new LinhaDaTabelaDeAfinidades(l.Afinidade, l.Eficiencia, l.Dano)).ToListAsync();
}
```

Migration:

```bash
dotnet ef migrations add AddTabelaDeAfinidades --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations
```

Confira que a migration só cria a tabela e o índice único (nenhuma alteração alheia).

`Program.cs`: `builder.Services.AddScoped<TabelaDeAfinidadesProvider>();` ao lado do provider da Durabilidade; nos dois blocos de seed, logo após o da Durabilidade:

```csharp
    var afinidadesSeedResult = await TabelaDeAfinidadesSeeder.SeedAsync(db, RulesDataProvider.ReadResource("Tabela de Afinidades.md"));
    app.Logger.LogInformation("TabelaDeAfinidades seed: {InsertedCount} new row(s) inserted", afinidadesSeedResult);
```

(no bloco `migrate`, use os nomes de variável daquele bloco — `migrateDb`, prefixo `migrate`.)

- [ ] **Step 4: Rodar** — `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~TabelaDeAfinidadesSeederTests` verde; `dotnet build` com 0 warnings.

- [ ] **Step 5: Commit** — `feat: tabela TabelaDeAfinidades com seed do Markdown e provider`

---

### Task 3: API — CRUD da Auditoria e fichas lendo a tabela

**Files:**
- Create: `src/RuinaRPG.Contracts/Rules/LinhaDaTabelaDeAfinidadesResponse.cs`, `src/RuinaRPG.Contracts/Rules/SalvarLinhaDaTabelaDeAfinidadesRequest.cs`
- Create: `src/RuinaRPG.Api/Controllers/TabelaDeAfinidadesController.cs`
- Modify: `src/RuinaRPG.Api/Services/CharacterSheetStats.cs`, `src/RuinaRPG.Api/Services/NpcSheetStats.cs`
- Modify: `src/RuinaRPG.Domain/CharacterSheets/SubAttributeFormulas.cs` (remover sobrecargas de um argumento e as constantes `PontosPor*`)
- Modify: `src/RuinaRPG.Client/Shared/SubAtributosSection.razor` (linhas ~35 e ~40 usam as constantes removidas)
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/TabelaDeAfinidadesControllerTests.cs`; atualizar `CharacterSheetsControllerTests.cs` (~1291-1310), `NpcAffinitiesControllerTests.cs` (~376-390), `tests/RuinaRPG.Tests.Unit/CharacterSheets/SubAttributeFormulasTests.cs` (~85), `tests/RuinaRPG.Tests.Client/Shared/SubAtributosSectionTests.cs`

**Interfaces:**
- Consumes: `TabelaDeAfinidadesProvider.LinhasAsync()`, `RuinaRpgDbContext.TabelaDeAfinidades`, `AfinidadeElementalLinha` (Task 2); fórmulas de dois argumentos (Task 1).
- Produces:
  - `public record LinhaDaTabelaDeAfinidadesResponse(Guid Id, int Afinidade, int Eficiencia, int Dano);`
  - `public record SalvarLinhaDaTabelaDeAfinidadesRequest(int Afinidade, int Eficiencia, int Dano);`
  - `GET api/tabela-de-afinidades` → `List<LinhaDaTabelaDeAfinidadesResponse>` ordenada por Afinidade (qualquer autenticado)
  - `POST api/tabela-de-afinidades` → 201 + `LinhaDaTabelaDeAfinidadesResponse`
  - `PUT api/tabela-de-afinidades/{id:guid}` → 204; `DELETE api/tabela-de-afinidades/{id:guid}` → 204
  - Erros: 400 e 409 com corpo de texto em português; 404; 403.

- [ ] **Step 1: Testes falhando do controller** — copie a estrutura (fixture, `AuthedRequest`, `RegisterGmAndGetTokenAsync`, `GrantRulesAuditorAsync`) de `tests/RuinaRPG.Tests.Integration/Controllers/DurabilidadesPorRankControllerTests.cs`. Todo teste que cria/edita/exclui restaura a tabela ao fim (o banco é compartilhado pela classe). Use valores de Afinidade altos (ex.: 900+) para as linhas criadas nos testes, para não colidir com as 22 semeadas. Casos:
  - `List_returns_the_rows_ordered_by_Afinidade_to_any_authenticated_user` (GM comum; 22 linhas, primeira Afinidade 0, última 21).
  - `Create_adds_a_row` (201, corpo com Id; aparece no GET).
  - `Create_rejects_a_repeated_Afinidade_with_409`.
  - `Create_rejects_negative_values_with_400` (Theory: cada um dos três campos em -1).
  - `Update_changes_a_row` (204) e `Update_keeping_its_own_Afinidade_is_not_a_conflict` (Review Focus 4).
  - `Update_to_another_rows_Afinidade_is_409`.
  - `Update_and_Delete_of_an_unknown_id_are_404`.
  - `Delete_removes_a_row` (204; some do GET).
  - `Create_Update_and_Delete_are_403_for_a_non_Auditor`.

- [ ] **Step 2: Rodar e ver falhar.**

- [ ] **Step 3: Implementar contracts e controller** — forma e comentário de classe no estilo de `DurabilidadesPorRankController` (mesmos `RequireRulesAuditorAsync`/`CurrentUserId`). Mensagens: 400 → `"Afinidade, Eficiência e Dano devem ser números inteiros maiores ou iguais a zero."`; 409 → `"Já existe uma linha para a Afinidade {n}."`. A checagem de duplicidade em PUT exclui a própria linha (`l.Afinidade == request.Afinidade && l.Id != id`). POST devolve `CreatedAtAction`/`Created` com a linha.

- [ ] **Step 4: Rodar** — `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~TabelaDeAfinidadesControllerTests` verde.

- [ ] **Step 5: Atualizar os testes das fichas (falhando primeiro)**
  - `CharacterSheetsControllerTests` ~1291: 7 pontos no elemento agora dão `EficienciaElemental 4` e `DanoElemental 3` (linha 7 da tabela); ajuste os comentários.
  - `NpcAffinitiesControllerTests` ~376: confira o valor de pontos usado no arranjo e troque as expectativas para a linha correspondente da tabela semeada (Eficiência = metade arredondada para cima, Dano = metade arredondada para baixo, até 21).
  - Novo, em `CharacterSheetsControllerTests` (Review Focus 5): `SubAttributes_follow_an_edit_made_by_the_Auditor_to_the_Tabela_de_Afinidades` — ficha com 7 pontos; Auditor faz PUT na linha de Afinidade 7 para Eficiência 40/Dano 30; `GET sub-attributes` devolve 40/30; restaure a linha (4/3) num `finally`.
  - Rode e veja falhar (ainda ÷2/÷3).

- [ ] **Step 6: Ligar as fichas à tabela** — `CharacterSheetStats` e `NpcSheetStats` recebem `TabelaDeAfinidadesProvider tabelaDeAfinidades` no construtor primário e usam:

```csharp
        var tabelaDeAfinidades = await this.tabelaDeAfinidades.LinhasAsync();
        // …
            EficienciaElemental: SubAttributeFormulas.EficienciaElemental(valorDaAfinidade, tabelaDeAfinidades),
            DanoElemental: SubAttributeFormulas.DanoElemental(valorDaAfinidade, tabelaDeAfinidades),
```

(nomeie o parâmetro/variável local de modo que não colidam; siga o estilo do arquivo.) Procure por `new CharacterSheetStats(`/`new NpcSheetStats(` em `src` e `tests` e ajuste as construções manuais, se houver.

- [ ] **Step 7: Remover a regra antiga** — apague de `SubAttributeFormulas` as sobrecargas de um argumento, as constantes `PontosPorEficienciaElemental`/`PontosPorDanoElemental` e o comentário da divisão; apague o teste unitário `EficienciaElemental_is_one_per_two_points_and_DanoElemental_one_per_three`. Em `SubAtributosSection.razor`, as duas legendas `@Regra(SubAttributeFormulas.PontosPor…)` passam a mostrar o texto fixo `conforme a Tabela de Afinidades`; remova o método `Regra` se ficar sem uso. Atualize `SubAtributosSectionTests.cs` primeiro (teste falhando) se ele verificar o texto antigo da legenda; se não verificar, acrescente um teste que espera `conforme a Tabela de Afinidades` nas duas legendas.

- [ ] **Step 8: Rodar** — `dotnet build` (0 warnings); `dotnet test tests/RuinaRPG.Tests.Unit`; `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~SubAtributos`; `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~TabelaDeAfinidades|FullyQualifiedName~SubAttributes|FullyQualifiedName~NpcAffinitiesControllerTests"` — tudo verde.

- [ ] **Step 9: Commit** — `feat: Auditoria da Tabela de Afinidades (API) e Eficiência/Dano Elemental das fichas pela tabela`

---

### Task 4: Client — página de Auditoria da Tabela de Afinidades

**Files:**
- Create: `src/RuinaRPG.Client/Pages/AuditoriaTabelaDeAfinidades.razor`
- Modify: `src/RuinaRPG.Client/Layout/RulesAuditorNavLinks.razor`
- Test: `tests/RuinaRPG.Tests.Client/Pages/AuditoriaTabelaDeAfinidadesTests.cs` (e o teste existente do `RulesAuditorNavLinks`, se houver)

**Interfaces:**
- Consumes: os endpoints e records da Task 3 (o `HttpClient` do client já tem a base `api/`: chame `"tabela-de-afinidades"`, como a página da Durabilidade chama `"durabilidades-por-rank"`).
- Produces: rota `/auditoria/tabela-de-afinidades`.

Modelos a seguir: `AuditoriaDurabilidadePorRank.razor` (estrutura, `Breadcrumbs`, `Section`, `InfoPopup`, `DismissibleAlert`, tratamento de 403 com `_forbidden`) e uma página de Auditoria com criar/excluir — `AuditoriaPericias.razor` ou `AuditoriaHistoricos.razor` — para o padrão de formulário de criação, confirmação de exclusão e seus testes bUnit (`tests/RuinaRPG.Tests.Client/Pages/AuditoriaPericiasTests.cs`). Use o mesmo mecanismo de confirmação e de mock de HTTP que esses testes já usam; não invente um novo.

Comportamento:
- Título `Auditoria: Tabela de Afinidades`; breadcrumb `Painel` → `Auditoria: Tabela de Afinidades`.
- `InfoPopup` "Como funciona a Tabela de Afinidades": `Eficiência Elemental e Dano Elemental das fichas vêm desta tabela, conforme o valor da essência ou elemento da Afinidade escolhida. Vale a linha de maior Afinidade que não ultrapassa o valor: abaixo da menor linha os dois valem 0, e acima da maior valem os valores dela. Toda alteração vale imediatamente para todas as fichas.`
- Tabela (`MudSimpleTable`) ordenada por Afinidade: colunas Afinidade, Eficiência, Dano e ações. Cada linha é editável no lugar com três `MudNumericField T="int"` (`Min="0"`), salvando por PUT a cada alteração (como a Durabilidade), e tem um `MudIconButton` de excluir (`Icons.Material.Filled.Delete`, com `aria-label="Excluir linha da Afinidade {n}"`) que pede confirmação antes do DELETE.
- Acima ou abaixo da tabela, um formulário "Nova linha" com os três campos (`Min="0"`) e o botão `Adicionar linha` (POST); depois de criar, limpa o formulário e recarrega.
- Resposta 403 → `_forbidden` e o mesmo alerta da Durabilidade. Outro erro → corpo da resposta no `DismissibleAlert` (ou `Não foi possível salvar a alteração.` se vazio) e recarrega a lista, descartando o valor rejeitado.
- Tabela sem linhas → texto `Nenhuma linha cadastrada — Eficiência e Dano Elemental valem 0 em todas as fichas.`
- `RulesAuditorNavLinks.razor`: novo link após "Durabilidade por Rank": `<MudNavLink Href="auditoria/tabela-de-afinidades" Icon="@Icons.Material.Filled.AutoAwesome" IconColor="Color.Primary">Tabela de Afinidades</MudNavLink>`.

- [ ] **Step 1: Testes bUnit falhando** — casos:
  - lista as linhas vindas do GET, na ordem recebida;
  - `Adicionar linha` faz POST com os três valores e recarrega;
  - alterar a Eficiência de uma linha faz PUT em `tabela-de-afinidades/{id}` com os três valores;
  - excluir + confirmar faz DELETE em `tabela-de-afinidades/{id}`; excluir + cancelar não chama a API;
  - resposta 409 no POST mostra a mensagem do corpo;
  - resposta 403 mostra o alerta de sem permissão;
  - lista vazia mostra o texto de tabela vazia;
  - o menu do Auditor tem o link `auditoria/tabela-de-afinidades`.
- [ ] **Step 2: Rodar e ver falhar** — `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~AuditoriaTabelaDeAfinidades`.
- [ ] **Step 3: Implementar a página e o link.**
- [ ] **Step 4: Rodar** — os testes acima verdes; `dotnet test tests/RuinaRPG.Tests.Client` inteiro verde; `dotnet build` com 0 warnings.
- [ ] **Step 5: Commit** — `feat(client): página de Auditoria da Tabela de Afinidades`

---

### Task 5: Versão 1.4.4, changelog e requisitos

**Files:**
- Modify: `src/RuinaRPG.Domain/AppVersionInfo.cs` (`Current = "1.4.4"`)
- Modify: `src/RuinaRPG.Client/Shared/ChangelogDialog.razor`
- Test: `tests/RuinaRPG.Tests.Client/Shared/ChangelogDialogTests.cs`
- Modify: `Docs/Requisitos/Requisitos - Auditoria de Regras.md` (R0016 novo; R0015), `Docs/Requisitos/Requisitos - Ficha de Personagem.md` (2.b), `Docs/Requisitos/Requisitos - Ficha de NPCs.md` (só se repetir a regra), `Docs/Requisitos/Requisitos - Modelo de Dados.md`, `Docs/Sistema RPG/Formulas.md` (linhas 14-15), `CLAUDE.md`

- [ ] **Step 1: Testes do diálogo falhando** — em `ChangelogDialogTests.cs`, troque as chamadas `RenderDialog("1.4.3", …)` para `"1.4.4"` onde o teste verifica o conteúdo da versão atual e ajuste/acrescente: a lista geral contém `Tabela de Afinidades`, `Anexos da campanha` e `Características Negativas`; o bloco `Para Auditores` (com `isRulesAuditor: true`) contém `Tabela de Afinidades` e não aparece para quem não é Auditor; o texto da 1.4.3 (`Disciplina`) não aparece mais. Rode e veja falhar.

- [ ] **Step 2: Implementar** — `AppVersionInfo.Current = "1.4.4"`; em `ChangelogDialog.razor`, atualize o comentário para 1.4.4 e substitua as duas listas pelo texto da seção "Versão 1.4.4" da spec (lista geral de 6 itens; "Para Auditores" com 1 item), no mesmo formato `<li><b>Título:</b> texto</li>`. Antes de fixar o texto, confira cada item contra `git log 5ebb154..HEAD --oneline` e o diff correspondente — o popup não pode afirmar algo que o código não faz.

- [ ] **Step 3: Rodar** — `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~ChangelogDialog` verde; `grep -rn "1\.4\.3" tests src` para achar expectativas presas à versão antiga e corrigir só as que se referem à versão **atual**.

- [ ] **Step 4: Requisitos**
  - `Requisitos - Auditoria de Regras.md`: acrescente ao fim `# **R0016** - O Auditor mantém a Tabela de Afinidades.` com **Descrição** cobrindo: página separada; cada linha tem Afinidade (única), Eficiência e Dano, inteiros ≥ 0; criar, editar e excluir; regra de consulta (maior linha que não ultrapassa; abaixo da menor ou sem linhas, 0; acima da maior, os valores dela); vale imediatamente para toda Ficha de Personagem e de NPC; valores iniciais de "[[Tabela de Afinidades]]", semeados só enquanto a tabela está vazia; não aparece no "[[Requisitos - Livro de Regras]]". Em R0015, acrescente a frase: `Na 1.4.4, descreve a página da Tabela de Afinidades (R0016).` Se o preâmbulo do documento enumerar as páginas de Auditoria, inclua a nova.
  - `Requisitos - Ficha de Personagem.md`: no item de Eficiência/Dano Elemental (procure `Eficiência Elemental`), troque a descrição das divisões por: os dois vêm de "[[Tabela de Afinidades]]", consultada com o Valor da linha de Afinidades (2.c) correspondente à Afinidade escolhida em 1.a, mantida pelo Auditor ("[[Requisitos - Auditoria de Regras]]" R0016). Preserve o restante do parágrafo (sem Afinidade → 0; aplicação manual).
  - `Requisitos - Ficha de NPCs.md`: `grep -n "Eficiência Elemental"`; ajuste apenas se a regra numérica estiver repetida lá.
  - `Formulas.md` linhas 14-15: `Eficiência elemental = coluna Eficiência de [[Tabela de Afinidades]], para o Valor da linha de Afinidades (2.c) correspondente à Afinidade escolhida (1.a)` e o análogo para `Dano elemental` (coluna Dano).
  - `Requisitos - Modelo de Dados.md`: nova tabela `TabelaDeAfinidades` (Id PK, Afinidade int único, Eficiencia int, Dano int), junto de onde `DurabilidadesPorRank` é descrita e no mesmo formato.
  - `CLAUDE.md`: na lista de arquivos embutidos, "Thirteen" → "Fourteen", acrescente `Tabela de Afinidades.md` e uma frase: como a Tabela de Níveis, ele só semeia `TabelaDeAfinidades` enquanto a tabela está vazia; depois vale a Auditoria (R0016).

- [ ] **Step 5: Verificação final** — `dotnet build` (0 warnings, 0 errors); `dotnet test tests/RuinaRPG.Tests.Unit`; `dotnet test tests/RuinaRPG.Tests.Client`; `grep -rn "Tabela_Afinidades\|PontosPorEficiencia\|PontosPorDano" --exclude-dir=bin --exclude-dir=obj --exclude-dir=.git . | grep -v "docs/superpowers"` sem resultados.

- [ ] **Step 6: Commit** — `feat: versão 1.4.4 — novidades no diálogo de versão e requisitos da Tabela de Afinidades`
