# Tabela de Afinidades e versão 1.4.4 — design

Data: 2026-10-09. Branch: `release/1.4.4`.

## Objetivo

Eficiência Elemental e Dano Elemental (Sub-Atributos, Ficha de Personagem 2.b e Ficha de NPC) deixam
de ser calculados por divisão (valor ÷ 2 e valor ÷ 3) e passam a ser consultados numa tabela editável
pelo Auditor de Regras. A mudança sai como versão 1.4.4, cujo diálogo de novidades cobre também o que
foi ao ar depois da 1.4.3 apenas como fix.

Decisões confirmadas com o usuário:

- A coluna **Afinidade** é o número atrelado a uma Essência Básica ou Elemento; **Eficiência** é a
  Eficiência Elemental; **Dano** é o Dano Elemental.
- Valor acima da maior linha da tabela: valem a Eficiência e o Dano da maior linha.
- Página de Auditoria com **criar, editar e excluir** linhas.
- Versão 1.4.4, com changelog acumulado desde a 1.4.3.

## Fonte dos dados

`Docs/Sistema RPG/Tabela_Afinidades.md` é renomeado para `Docs/Sistema RPG/Tabela de Afinidades.md`
(padrão das demais tabelas) e ganha um parágrafo de introdução antes da tabela, no molde de
`Tabela de Durabilidade por Rank.md`. As 22 linhas (Afinidade 0 a 21) ficam como estão:

| Afinidade | Eficiência | Dano |
| --------- | ---------- | ---- |
| 0 | 0 | 0 |
| 1 | 1 | 0 |
| 2 | 1 | 1 |
| … | … | … |
| 21 | 11 | 10 |

O arquivo é embutido em `RuinaRPG.Infrastructure.csproj` com `LogicalName="Tabela de Afinidades.md"`.

## Regra de consulta (Domain)

Entrada: o valor da afinidade correspondente (já resolvido por
`SubAttributeFormulas.ValorDaAfinidadeCorrespondente`, que não muda) e as linhas da tabela.

- Vale a linha de **maior Afinidade que não ultrapassa o valor**.
- Valor abaixo da menor Afinidade da tabela, ou tabela vazia: Eficiência 0 e Dano 0.
- Valor acima da maior Afinidade: vale a maior linha (consequência da primeira regra).
- A ordem das linhas na entrada não importa.

Forma:

- `RuinaRPG.Domain.CharacterSheets.LinhaDaTabelaDeAfinidades` —
  `readonly record struct (int Afinidade, int Eficiencia, int Dano)`.
- `SubAttributeFormulas.EficienciaElemental(int valor, IReadOnlyList<LinhaDaTabelaDeAfinidades> tabela)`
  e `SubAttributeFormulas.DanoElemental(int valor, IReadOnlyList<LinhaDaTabelaDeAfinidades> tabela)`
  substituem as sobrecargas de um argumento.
- As constantes `PontosPorEficienciaElemental` e `PontosPorDanoElemental` são removidas, junto com
  qualquer uso delas (inclusive textos de ajuda no client, se houver).
- `RuinaRPG.Domain.Rules.TabelaDeAfinidadesParser.Parse(string markdown)` devolve as linhas da tabela
  do Markdown (mesmo estilo de `TabelaDeDurabilidadeParser`).

## Persistência (Infrastructure)

- Entidade `RuinaRPG.Infrastructure.CharacterSheets.AfinidadeElementalLinha`:
  `Id` (Guid, PK), `Afinidade` (int, índice único), `Eficiencia` (int), `Dano` (int).
- `DbSet<AfinidadeElementalLinha> TabelaDeAfinidades` em `RuinaRpgDbContext`; tabela
  `TabelaDeAfinidades`. Migration `AddTabelaDeAfinidades`.
- `TabelaDeAfinidadesSeeder.SeedAsync(db, markdown)`: insere as linhas do Markdown **só quando a
  tabela está vazia**; devolve quantas inseriu. Não usa "inserir as que faltam" (como a Durabilidade
  por Rank), porque isso ressuscitaria linhas excluídas pelo Auditor. Consequência aceita: se o
  Auditor excluir todas as linhas, a próxima subida da API (Development) ou `make migrate`
  (Production) repõe as 22 linhas originais.
- O seeder é chamado nos dois pontos de `Program.cs` onde `DurabilidadePorRankSeeder` já é chamado
  (caminho `migrate` e startup em Development), com log da contagem.
- `TabelaDeAfinidadesProvider` (scoped): carrega as linhas uma vez por escopo, `AsNoTracking`, e as
  entrega como `IReadOnlyList<LinhaDaTabelaDeAfinidades>`.

## API

`TabelaDeAfinidadesController`, rota `api/tabela-de-afinidades`, `[Authorize]`:

| Verbo | Rota | Quem | Resultado |
|---|---|---|---|
| GET | `/` | qualquer autenticado | linhas ordenadas por Afinidade |
| POST | `/` | Auditor de Regras | 201 com a linha criada |
| PUT | `/{id}` | Auditor de Regras | 204 |
| DELETE | `/{id}` | Auditor de Regras | 204 |

- Autorização do Auditor igual à de `DurabilidadesPorRankController` (consulta
  `ApplicationUser.IsRulesAuditor` no banco; 403 caso contrário).
- Validação (400): Afinidade, Eficiência e Dano são inteiros ≥ 0.
- Afinidade já usada por outra linha: 409 com mensagem em português.
- Id inexistente em PUT/DELETE: 404.

Contracts (`RuinaRPG.Contracts.Rules`): `LinhaDaTabelaDeAfinidadesResponse(Guid Id, int Afinidade,
int Eficiencia, int Dano)` e `SalvarLinhaDaTabelaDeAfinidadesRequest(int Afinidade, int Eficiencia,
int Dano)` (usado por POST e PUT).

`CharacterSheetStats` e `NpcSheetStats` recebem o `TabelaDeAfinidadesProvider` e passam as linhas às
duas fórmulas. `CreatureSheetStats` não muda (Criatura não tem esses campos). O formato de
`SubAttributesResponse` não muda.

## Client

- Página `AuditoriaTabelaDeAfinidades.razor`, rota `auditoria/tabela-de-afinidades`, só para o
  Auditor de Regras (mesma proteção das outras páginas de Auditoria).
- Tabela ordenada por Afinidade com as colunas Afinidade, Eficiência, Dano e ações (editar, excluir).
- Botão "Nova linha" abre o mesmo formulário usado na edição (três campos numéricos, mínimo 0).
- Excluir pede confirmação. Erros 400/409 da API aparecem como mensagem (snackbar), no padrão das
  outras páginas de Auditoria.
- Entrada em `RulesAuditorNavLinks.razor`: "Tabela de Afinidades", com ícone MudBlazor (sem emojis).
- A ficha de Personagem/NPC não muda de layout — só os números.

## Requisitos (`Docs/`)

- `Requisitos - Auditoria de Regras.md`: **R0016** — o Auditor mantém a Tabela de Afinidades (CRUD,
  unicidade da Afinidade, regra de consulta, efeito imediato em todas as fichas, valores iniciais
  vindos de "[[Tabela de Afinidades]]", fora do Livro de Regras). R0015 ganha a frase da 1.4.4.
- `Requisitos - Ficha de Personagem.md` (2.b): Eficiência e Dano Elemental passam a citar
  "[[Tabela de Afinidades]]" em vez das divisões; idem onde `Requisitos - Ficha de NPCs.md` repetir a
  regra.
- `Docs/Sistema RPG/Formulas.md`: as duas linhas apontam para "[[Tabela de Afinidades]]".
- `Requisitos - Modelo de Dados.md`: tabela `TabelaDeAfinidades`.
- `CLAUDE.md`: a lista de arquivos embutidos passa a ter 14, com a observação de que
  `Tabela de Afinidades.md` só semeia enquanto a tabela está vazia.

Números da tabela não são repetidos nos requisitos — eles apontam para o arquivo-fonte.

## Versão 1.4.4

`AppVersionInfo.Current = "1.4.4"`. `ChangelogDialog.razor` troca a lista pela da 1.4.4:

Lista geral:

- **Afinidade Elemental:** Eficiência Elemental e Dano Elemental agora vêm da Tabela de Afinidades,
  conforme o valor da essência ou elemento da Afinidade escolhida (os valores das fichas podem mudar).
- **Sub-Atributos:** organizados em blocos, com a Defesa em coluna à direita e a Afinidade Elemental
  no fim; o estado da Cobertura ganhou uma cor de maior contraste.
- **Características:** o limite de Características Negativas passa a ser o dobro do de Positivas.
- **Anexos da campanha:** agrupados por tipo, com galeria de imagens e filtros por grupo; a busca não
  lista mais o que já está anexado.
- **Catálogo de Itens:** os campos de Subcategoria sugerem as subcategorias já existentes; Requisitos
  e Penalidade do equipamento aparecem no popup do item, e não mais na lista.
- **Banco de Magias e Habilidades:** filtros de Categoria, Vocação e Classe nas Passivas.

Para Auditores:

- **Tabela de Afinidades:** nova página de Auditoria para criar, editar e excluir as linhas que
  definem Eficiência e Dano Elemental por valor de Afinidade; acima da maior linha valem os valores
  dela.

O texto final de cada item é conferido contra os commits `5ebb154..HEAD` durante a implementação.

## Testes (TDD — Técnico R0011)

- Unit: consulta (valor exato, entre linhas com lacuna, abaixo da menor, acima da maior, tabela
  vazia, linhas fora de ordem); parser do Markdown (22 linhas do arquivo real).
- Integration: seeder (insere com a tabela vazia, não insere com linhas existentes); controller
  (lista ordenada, CRUD, 400, 409, 404, 403 para não-Auditor); Sub-Atributos de Personagem e de NPC
  refletindo a tabela, inclusive depois de o Auditor editar uma linha; migration.
- Client (bUnit): página de Auditoria (lista, criar, editar, excluir com confirmação) e
  `ChangelogDialog` com o texto da 1.4.4.
- Testes existentes que fixam ÷2/÷3 são atualizados para os valores da tabela.

Critério de conclusão: `dotnet build` com 0 avisos e 0 erros; `dotnet test` verde (o flake conhecido
dos testes de integração é tratado rodando os testes que falharem um a um).

## Fora de escopo

- Ficha de Criatura (não tem Eficiência/Dano Elemental).
- Exibir a tabela no Livro de Regras/Compêndio.
- Deploy: fica com o usuário; exige `make migrate` (migration nova).
