# Habilidades Raciais editáveis pelo GM — Design

## Contexto

`RacialAbilityLookup` (`src/RuinaRPG.Domain/CharacterSheets/RacialAbilityLookup.cs`) é um dicionário
hardcoded, uma entrada por `Variante` (7 valores: Sinir/Laonir = Humano, PhylacTai/EsPhylauc = Phylauc,
Yavos/Koroanos = Nephrytes, Alora = Ecônos), cada uma com `Nome`+`Descricao` fixos, espelhando
"Ruína RPG - Sistema Básico.md" §7. `CharacterSheetsController`/`NpcSheetsController` expõem
`GET .../racial-ability`, que só chama `RacialAbilityLookup.For(sheet.Variante.Value)` — sem
persistência, sem participação do GM.

Sinir e Laonir (Humano) são um caso especial: a habilidade racial deles é "Role 1d18 na tabela de
Arcas" — uma tabela de 18 entradas que **não existe em nenhum lugar dos docs** (confirmado por busca
em `Docs/Sistema RPG/`). É conteúdo livre do GM, não uma regra fixa do sistema.

## Objetivo

1. O GM pode cadastrar/editar a tabela de Arcas (18 linhas, uma por resultado de 1d18).
2. O GM pode sobrescrever o Nome/Descrição da habilidade racial de qualquer uma das 7 Variantes
   (o valor de `RacialAbilityLookup` vira o *padrão*, não mais o único valor possível).
3. Na Ficha de Personagem/NPC, quando a Linhagem for **Humano** (independente da Variante Sinir/
   Laonir), o jogador digita o número que tirou no d18; a ficha resolve e mostra a Arca
   correspondente da tabela do GM.
4. Fora de escopo (decidido em brainstorming): o app nunca rola dados por ninguém — isso é só
   referência/registro manual. Criatura não tem habilidade racial (já confirmado no código
   existente — não é tocada por este trabalho).

## Modelo de dados

Duas tabelas novas, ambas escopadas por GM (biblioteca do GM, mesmo padrão de `Item`/`Trait`):

```
RacialAbilityOverride
  Id            Guid PK
  GmId          Guid FK -> AspNetUsers
  Variante      Variante (enum, armazenado como string via HasConversion, mesmo padrão de outros
                enums neste DbContext)
  Nome          string
  Descricao     string
  Unique(GmId, Variante)

ArcaEntry
  Id            Guid PK
  GmId          Guid FK -> AspNetUsers
  Roll          int (1-18)
  Nome          string
  Descricao     string
  Unique(GmId, Roll)
```

`RacialAbilityOverride` só existe quando o GM efetivamente sobrescreveu aquela Variante — ausência de
linha = usa `RacialAbilityLookup.For(variante)`. `ArcaEntry` só existe para os rolls que o GM já
preencheu — ausência de linha para um roll = "Arca não cadastrada", não erro.

`CharacterSheet`/`NpcSheet` ganham um campo novo:

```
ArcaRolada   int?   // 1-18, null = ainda não rolou. Só tem efeito quando Linhagem == Humano.
```

Migração: `AddRacialAbilityOverrideAndArcaEntry` (as 2 tabelas novas) + a coluna `ArcaRolada` nas 2
tabelas de ficha já existentes.

## API

Novo `RacialAbilitiesController` (`api/racial-abilities`, `api/arcas`):

- `GET api/racial-abilities` — as 7 Variantes já resolvidas: `RacialAbilityEntryResponse(string
  Variante, string Nome, string Descricao, bool IsDefault)`. Leitura via `ResolveEffectiveGmIdAsync`
  (mesmo padrão de `ItemsController`/`TraitsController` — GM vê a própria, jogador vê a do GM
  vinculado), porque a ficha também lê daqui.
- `PUT api/racial-abilities/{variante}` (GM-only) — body `UpdateRacialAbilityRequest(string Nome,
  string Descricao)`. Upsert do override.
- `DELETE api/racial-abilities/{variante}` (GM-only) — remove o override (volta ao padrão).
- `GET api/arcas` — sempre 18 entradas (`ArcaEntryResponse(int Roll, string? Nome, string?
  Descricao)`), preenchidas do banco ou `null`/`null` quando o GM não cadastrou aquele roll. Mesma
  regra de leitura.
- `PUT api/arcas/{roll}` (GM-only, `roll` 1-18, 400 fora da faixa) — body `UpdateArcaEntryRequest(string
  Nome, string Descricao)`. Upsert daquela linha.

`CharacterSheetsController.RacialAbility`/`NpcSheetsController.RacialAbility` (existentes):
- Resolvem `RacialAbilityOverride` (pelo GM da ficha) antes de cair no `RacialAbilityLookup` default.
- Quando `sheet.Linhagem == Linhagem.Humano`, a resposta ganha `ArcaRolada` (ecoado da ficha),
  `ArcaNome`/`ArcaDescricao` (resolvidos de `ArcaEntry` pelo `Roll == sheet.ArcaRolada`, `null` se não
  rolou ou se o GM não cadastrou aquele número). `RacialAbilityResponse` vira:
  `RacialAbilityResponse(string? Nome, string? Descricao, int? ArcaRolada, string? ArcaNome, string?
  ArcaDescricao)`.

`UpdateCharacterSheetRequest`/`UpdateNpcSheetRequest` ganham `int? ArcaRolada` (nullable — nem todo
personagem é Humano, e mesmo um Humano pode não ter rolado ainda). `CharacterSheetResponse`/
`NpcSheetResponse` ecoam o mesmo campo, para o form da ficha carregar o valor já salvo.

## Cliente

**Nova página `HabilidadesRaciais.razor`** (GM-only, rota `/habilidades-raciais`, link no `NavMenu`
junto de Catálogo/Banco de Magias):
- As 5 Variantes "simples" (PhylacTai, EsPhylauc, Yavos, Koroanos, Alora), cada uma num `<Section>`
  com Nome/Descrição editáveis (autosave-on-blur, `AutoSaveCoordinator`/`AutoSaveIndicator` — mesmo
  padrão já usado em `CatalogoItemForm`) e um botão "Restaurar padrão" (chama o DELETE, recarrega).
- Uma seção "Sinir / Laonir (Humano)": Nome/Descrição editáveis das duas Variantes (mesma UI, cada
  uma seu próprio autosave) + logo abaixo, "Tabela de Arcas": uma tabela de 18 linhas fixas (Roll 1 a
  18), cada linha com Nome/Descrição editáveis inline (blur salva aquela linha via `PUT
  api/arcas/{roll}`).

**Ficha de Personagem/NPC** (`FichaDePersonagem.razor`/`FichaDeNpc.razor`, seção "Habilidade
Racial"): mantém o texto Nome/Descrição atual (agora vindo do override quando existir). Quando
`Linhagem == "Humano"` (checagem client-side sobre o valor já carregado da ficha, independente da
Variante), aparece um `MudNumericField` "Número rolado (1d18)" ligado a `ArcaRolada` — segue o mesmo
idioma de `Ciclos`/`Cobertura` (campo no `_form` compartilhado, `ValueChanged` → grava no form →
`SaveIgnoringResultAsync()` → recarrega `racial-ability`). Abaixo do campo, mostra a Arca resolvida
(`ArcaNome`/`ArcaDescricao` da resposta) ou "Arca não cadastrada." quando o GM não preencheu aquele
número.

## Fora de escopo

- Rolagem automática de dados (decidido em brainstorming).
- Qualquer mudança em Criatura (não tem habilidade racial).
- Um "reset em massa" de todos os overrides de uma vez — restaurar padrão é por Variante.

## Testes

TDD em toda a parte de backend (Domain: nenhuma lógica nova de cálculo além de um lookup simples,
não precisa de calculator dedicado; Integration: novos testes para os 5 endpoints de
`RacialAbilitiesController` incl. limites de `roll` fora de 1-18, e para a resolução
override-antes-do-padrão + Arca resolvida nos 2 endpoints `racial-ability` existentes — casos: sem
override, com override, Humano sem rolar, Humano com roll não cadastrado, Humano com roll
cadastrado). Cliente: markup-only nas fichas existentes (sem bUnit novo, mesmo precedente de
`Ciclos`/`Cobertura`); a nova página `HabilidadesRaciais.razor` segue o padrão de
`CatalogoItemForm`/`BancoDeMagiasForm` — sem bUnit dedicado hoje nesse tipo de página de
cadastro-simples (nenhuma das duas tem).
