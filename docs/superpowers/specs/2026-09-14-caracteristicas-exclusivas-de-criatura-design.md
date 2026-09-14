# Características exclusivas de Criatura — Design

## Contexto

Personagem, NPC e Criatura hoje compartilham um único catálogo de Características —
`Trait` (`src/RuinaRPG.Infrastructure/Rules/Trait.cs`), seedado a partir de
`Docs/Sistema RPG/Características.md`, CRUD restrito ao Auditor de Regras via
`TraitsController` (`api/traits`). Três pontos relevantes confirmados no código:

- `CompendioController.Search` (o motor de busca do Livro de Regras) lê **direto de
  `db.Traits`** a cada chamada (não do markdown estático) — qualquer linha nova nessa
  tabela aparece automaticamente no Livro de Regras.
- `CharacterPossessionsController`/`NpcPossessionsController`/`CreaturePossessionsController`
  resolvem `AddTrait` contra a mesma `db.Traits`, sem nenhuma distinção por tipo de ficha.
- `CreatureTrait` (`src/RuinaRPG.Infrastructure/CreatureSheets/CreatureTrait.cs`) guarda só
  `TraitId` + `Especificacao` — Nome/Descrição/Custo/Polaridade são resolvidos por join contra
  `Traits` a cada leitura (não é cópia independente).

O jogo tem características que só fazem sentido em criaturas (não em Personagem/NPC) e que
não devem poder ser encontradas no Livro de Regras. Adicioná-las em `Traits` vazaria para os
três pontos acima; por isso este design usa uma tabela genuinamente separada.

## Objetivo

1. Uma nova tabela de características, exclusiva para Ficha de Criatura, com o mesmo formato
   de `Trait` (Nome, Descrição, Custo, Polaridade, exige Especificação).
2. Uma nova página de Auditoria (Auditor de Regras) para adicionar/editar/remover essas
   características — separada da página de Auditoria de Características existente.
3. No seletor de característica da Ficha de Criatura, as duas origens aparecem juntas numa
   lista só.
4. Uma característica exclusiva de criatura concedida a uma ficha conta no mesmo orçamento de
   pontos (Positivas/Negativas) que as características normais — mesmo teto, mesma regra.
5. Estrutural, não por filtro: o Livro de Regras/Compêndio e os seletores de Personagem/NPC
   nunca devem conseguir enxergar essas características, porque nunca leem a tabela nova.

## Modelo de dados

Nova tabela, catálogo global (mesmo modelo de `Trait` — não é biblioteca por-GM):

```
CreatureExclusiveTrait
  Id                  Guid PK
  Nome                string
  Descricao           string
  Custo               int
  Polaridade          Polaridade (enum, mesma conversão já usada em Trait)
  RequerEspecificacao bool
  IsDeleted           bool   // soft delete — mesma razão de Trait.IsDeleted: uma ficha pode
                             // já ter essa característica concedida, apagar não pode quebrar
                             // esse join
  UpdatedByUserId     Guid?
  UpdatedAt           DateTime?
```

Sem `IsCustomized` — essa flag em `Trait` existe só para o `TraitSeeder` saber não sobrescrever
uma edição manual; `CreatureExclusiveTrait` não tem seeder nenhum (toda linha nasce manual, via
Auditor), então a flag não tem propósito aqui.

`CreatureTrait` ganha uma coluna:

```
IsCriaturaExclusiva  bool  // false = TraitId aponta para Traits (padrão); true = aponta para
                           // CreatureExclusiveTraits. Decidido uma vez, no momento do Add.
```

Migração: `AddCreatureExclusiveTraits` (tabela nova + a coluna em `CreatureTraits`).

## API

Novo `CreatureExclusiveTraitsController` (`api/creature-exclusive-traits`) — espelha
`TraitsController` quase linha a linha:

- `GET` (aberto a qualquer autenticado, `?nome=` filtra por `ILIKE`) — usado pelo seletor da
  Ficha de Criatura e pela própria página de Auditoria.
- `POST`/`PUT {id}`/`DELETE {id}` — `RequireRulesAuditorAsync()` (mesma checagem direta contra
  `ApplicationUser.IsRulesAuditor`, não claim de JWT). Mesma validação de sinal do Custo vs.
  Polaridade, mesmo bloqueio de duplicata (Nome+Custo+Polaridade). `DELETE` retorna 409 se
  `db.CreatureTraits.AnyAsync(t => t.TraitId == id && t.IsCriaturaExclusiva)` — só fichas de
  criatura podem referenciar essa tabela, então o filtro `IsCriaturaExclusiva` já é suficiente
  (sem precisar checar `CharacterTraits`/`NpcTraits`, que nunca gravam essa flag como true).

`CreaturePossessionsController` (as únicas mudanças de comportamento deste design):

- **`AddTrait`**: tenta resolver `request.TraitId` primeiro em `db.Traits`; se não achar, tenta
  em `db.CreatureExclusiveTraits`; se não achar em nenhuma, 400 (mesma mensagem de hoje). A
  validação de `RequerEspecificacao` e o cálculo de orçamento (abaixo) usam os campos
  resolvidos, não importa a origem. `CreatureTrait.IsCriaturaExclusiva` é gravado conforme onde
  o Id foi encontrado.
- **`ListTraits`/o `SumAsync` de orçamento em `AddTrait`**: como `CreatureTrait` agora pode
  apontar para duas tabelas diferentes, o `.Join(db.Traits, ...)` único de hoje vira uma
  resolução em duas etapas — carrega as linhas de `CreatureTrait` da ficha, separa por
  `IsCriaturaExclusiva`, busca cada grupo na tabela certa (`db.Traits`/`db.CreatureExclusiveTraits`,
  cada uma uma única query `Where(id => idsDoGrupo.Contains(...))`, sem N+1) e junta os
  resultados em memória antes de somar/montar `CreatureTraitResponse`. `TraitPointBudgetCalculator`
  em si não muda — ele só depende do total já calculado.

Nada muda em `TraitsController`, `CompendioController`, `CharacterPossessionsController` ou
`NpcPossessionsController` — a garantia do requisito 5 (nunca aparece no Livro de Regras nem
nos seletores de Personagem/NPC) vem de esses três nunca ganharem uma linha de código que leia
`CreatureExclusiveTraits`, não de um filtro.

## Cliente

**`FichaDeCriatura.razor`** — `SearchTraitsAsync` (a função que alimenta o `EntityPicker` de
"Buscar característica...") passa a chamar `api/traits?nome=` e
`api/creature-exclusive-traits?nome=` em paralelo, concatenar os dois resultados e ordenar a
lista final por Nome antes de devolver ao picker — uma lista só, sem rótulo de origem, ordem
alfabética previsível independente de qual endpoint respondeu primeiro. `AddTraitAsync` não
muda — já envia só o `TraitId` escolhido, e o servidor resolve a origem sozinho.

**Nova página `AuditoriaCaracteristicasDeCriatura.razor`** (rota
`/auditoria/caracteristicas-de-criatura`), cópia estrutural de `AuditoriaCaracteristicas.razor`
(mesmo formulário de adicionar, mesmas duas tabelas Positivas/Negativas com edição inline
blur-a-blur, mesmo tratamento de 403/409), apontando para `api/creature-exclusive-traits`.

**`RulesAuditorNavLinks.razor`** ganha um terceiro link, ao lado de "Auditoria: Livro de Regras"
e "Auditoria: Características":

```
<MudNavLink Href="auditoria/caracteristicas-de-criatura">Auditoria: Características de Criatura</MudNavLink>
```

## Fora de escopo

- Qualquer mudança em Personagem/NPC — não ganham acesso a essas características, por design.
- Um indicador visual no picker distinguindo as duas origens (aparecem misturadas, sem rótulo
  extra) — não pedido, YAGNI.
- Migrar características já existentes em `Traits` para a tabela nova — este design só cobre
  característica **nova**, criada pelo Auditor diretamente na tabela exclusiva.

## Testes

TDD em toda a parte de backend:

- **`CreatureExclusiveTraitsControllerTests`** (novo, espelha o já existente
  `TraitsControllerTests.cs`): List aberto a qualquer autenticado; Create/Update/Delete
  403 para não-Auditor, 200/204 para Auditor; validação de sinal do Custo; bloqueio de
  duplicata; Delete 409 quando em uso por uma `CreatureTrait`.
- **`CreaturePossessionsControllerTests`** (extensão dos testes de `traits` já existentes):
  adicionar uma característica exclusiva de criatura grava `IsCriaturaExclusiva=true` e resolve
  Nome/Descrição/Custo corretamente na resposta; orçamento de pontos soma característica normal
  + exclusiva juntas no mesmo teto (Positivas e Negativas); `RequerEspecificacao` rejeita sem
  Especificação também para a origem exclusiva.
- **Regressão no Compêndio**: um novo teste no já existente `CompendioControllerTests.cs`
  confirma que uma característica cadastrada só em `CreatureExclusiveTraits` **nunca** aparece
  em `GET api/compendio/search`, mesmo buscando pelo Nome exato.
- **Regressão em Personagem/NPC**: um teste confirma que `POST character-sheets/{id}/traits` /
  `POST npc-sheets/{id}/traits` com o Id de uma característica exclusiva de criatura retorna 400
  ("Trait não encontrado") — não pode ser concedida fora de Criatura.

Cliente: sem bUnit dedicado para a nova página de Auditoria (mesmo precedente de
`AuditoriaCaracteristicas.razor`, que também não tem) nem para o merge de busca em
`SearchTraitsAsync` (função privada trivial, comportamento coberto pelos testes de integração
dos dois endpoints que ela consome).
