# Banco de Runas — Design

## Contexto

Uma Runa hoje é só uma linha na aba "Magias & Habilidades" (4.d) das fichas de **Personagem** e **NPC**
(`CharacterRune` / `NpcRune`: `Nome`, `Descricao`, `Grau`; a Ficha de Criatura não tem Runas).
Não existe catálogo: cada Runa é digitada à mão, ficha a ficha.

O **Banco de Magias e Habilidades** (`Docs/Requisitos/Requisitos - Banco de Magias e Habilidades.md`,
`SpellAbilityBank*`) resolve exatamente esse problema para Magias/Habilidades: uma biblioteca por GM que
alimenta as fichas e se integra à Campanha. Este trabalho cria o equivalente para Runas.

## Objetivo

Espelhar o Banco de Magias para Runas, **por completo** (decisão do usuário):

1. Toda Runa criada numa ficha vai automaticamente para o banco do GM.
2. O GM cria e mantém entradas direto no banco (lista, filtros, edição, exclusão).
3. Ao adicionar uma Runa numa ficha, dá para partir de uma entrada do banco.
4. Integração com a Campanha: o GM anexa entradas (público/privado por anexo), e uma Runa criada por um
   jogador vira anexo público automaticamente.

**Os campos principais da Runa não mudam** (restrição do usuário): `Nome`, `Descrição`, `Grau`. Nenhum
campo extra é necessário e nenhum é adicionado.

Fora de escopo, deliberadamente:

- **Ficha de Criatura** — continua sem Runas (`Requisitos - Ficha de Criaturas.md`, R0004).
- **Runas que já existem nas fichas** — o banco **começa vazio**; não há migração de dados (decisão do
  usuário; igual ao que o Banco de Magias fez ao nascer).
- **Limite de Grau** — o doc de 4.d diz que o Grau é "limitado ao Grau atual do personagem", mas o código
  atual (`CharacterRunesController`/`NpcRunesController`) não valida isso. O banco espelha o comportamento
  existente e não introduz a regra.
- **Generalizar os dois bancos** numa abstração comum — descartado (ver "Abordagem").

## Abordagem

Três opções foram consideradas:

- **A. Pilha paralela e dedicada (escolhida).** `RuneBankEntry`, controller próprio e um novo alvo de
  anexo de campanha, espelhando o Banco de Magias sem alterá-lo. Previsível, segue o padrão do repositório;
  o custo é código parecido em dois lugares.
- **B. Generalizar os dois bancos** numa abstração comum: refatora e migra dados de um subsistema que
  funciona, para uma entidade de 3 campos. Não compensa.
- **C. Reaproveitar `SpellAbilityBankEntry` com Tipo "Runa":** Efeitos, Gasto em PI, Custo e o chip de Tipo
  não fazem sentido para Runas; suja o banco de magias.

## Modelo de dados

```
RuneBankEntry
  Id         Guid PK
  GmId       Guid FK -> AspNetUsers (cascade)     // biblioteca do GM, como SpellAbilityBankEntry
  Nome       string (required)
  Descricao  string (required)
  Grau       int
```

Alterações em tabelas existentes:

- `CharacterRune` e `NpcRune` ganham `SourceBankEntryId Guid?` — só rastreio de origem, **sem FK**, igual a
  `CharacterSpellAbility`/`NpcSpellAbility` (apagar a entrada do banco não afeta a Runa da ficha: são
  cópias independentes).
- `CampaignAttachment` ganha `RuneBankEntryId Guid?` (FK para `RuneBankEntry`), e
  `CampaignAttachmentTarget` ganha o valor `RuneBankEntry`. `CampaignAttachmentTargetValidator.ExactlyOneSet`
  passa a considerar 6 alvos em vez de 5.

Uma migration (`AddRuneBank`): a tabela nova e as três colunas. Nenhum backfill.

## API

### `api/rune-bank` (novo `RuneBankController`)

- `POST` — cria entrada (**GM-only**). Body `{Nome, Descricao, Grau}`.
- `GET` — lista com filtros `nome` (ILike, contém) e `grau` (igual), escopada ao GM da conta.
  **GM-only.** *Diferença deliberada em relação ao banco de magias*, cujo `GET` resolve o GM efetivo e deixa
  um jogador ler o banco inteiro do GM. Para Runas, o jogador só enxerga o que foi anexado como público
  (`available-runes`, abaixo) — o banco privado do GM não vaza.
- `PUT {id}` e `DELETE {id}` — **GM-only**, restritos às entradas do próprio GM (404 fora disso).

Validação: nenhuma regra nova além das que a Runa de ficha já tem (o modelo exige `Nome` e `Descricao` não
nulos; sem checagem de Grau).

### Fichas — `POST .../runes` (Personagem e NPC)

`AddCharacterRuneRequest` e `AddNpcRuneRequest` passam a ser
`(string? SourceBankEntryId, string? Nome, string? Descricao, int? Grau)`, mesmo formato dos requests de
Magia/Habilidade. Exatamente um caminho é aceito, senão 400:

- **do zero:** `Nome`, `Descricao` e `Grau` preenchidos;
- **do banco:** `SourceBankEntryId` preenchido — copia `Nome`/`Descricao`/`Grau` da entrada. Entrada
  inexistente, de outro GM ou id malformado → 400 ("Entrada do banco não encontrada.").
  Um jogador só pode usar entradas **anexadas como públicas** à campanha da ficha; o GM, qualquer entrada
  dele.

Em toda criação (do zero ou do banco), além da Runa na ficha (com `SourceBankEntryId` quando veio do banco),
grava-se uma **cópia independente** no banco do GM (`GmId` = GM da campanha, no Personagem; `sheet.GmId`, no
NPC). Quando quem cria **não é o GM** (é o jogador dono), a cópia também vira `CampaignAttachment` **público**
(`Requisitos - Campanha` R0012): no Personagem, da campanha da ficha; no NPC concedido a um jogador, da
campanha resolvida pelo vínculo de concessão — exatamente a regra que `NpcSpellAbilitiesController` já usa.

Escolher uma entrada do banco também cria uma nova cópia (e, para jogador, um novo anexo público) — o mesmo
comportamento de `Requisitos - Banco de Magias e Habilidades` R0001/R0003, mantido de propósito para o
espelhamento ficar fiel, mesmo que gere entradas repetidas.

`PUT`/`GET`/`DELETE` de Runas nas fichas não mudam (a edição na ficha não propaga ao banco).

### Campanha

- `CampaignAttachmentsController`: `AttachToCampaignRequest` ganha `RuneBankEntryId` (parâmetro novo, no
  fim, opcional) e o controller valida que a entrada existe e pertence ao GM da campanha. O toggle
  público/privado individual (R0008) vale para esse tipo como para os demais. `CampaignAttachmentResponse`
  devolve o tipo `"RuneBankEntry"` e o `Nome` da Runa.
- `CampaignCatalogController`: novo `GET campaigns/{campaignId}/available-runes` — as entradas anexadas como
  públicas, com filtro `nome`; mesma checagem de membership de `available-spell-abilities`. É o que o
  jogador usa no seletor de 4.d.
- `CampaignPlayerViewController`: os anexos públicos do tipo Runa aparecem em `anexosPublicos` como
  `"RuneBankEntry"` com o nome.

## Cliente

- **`/banco-de-runas`** (novo, GM): lista com filtros Nome e Grau, botão "Nova Entrada", editar e excluir;
  formulário de criação/edição (Nome, Descrição, Grau). Link "Banco de Runas" no `NavMenu` do GM, ao lado
  do de magias.
- **4.d nas fichas de Personagem e NPC:** o formulário de adicionar Runa ganha "Origem": *Montar do zero* ou
  *Escolher do Banco de Runas*. O GM carrega `rune-bank`; o jogador, `campaigns/{id}/available-runes`. Ao
  escolher do banco, o formulário mostra a entrada e envia só o `SourceBankEntryId`.
- **`CampanhaDetalhe`:** seção "Entrada do Banco de Runas" para anexar, e a lista de anexos passa a exibir
  o tipo Runa com o toggle público/privado.
- **Visão do jogador da campanha** (`MinhaCampanha`): mostra as Runas públicas junto dos demais anexos.

## Documentação

- **Novo** `Docs/Requisitos/Requisitos - Banco de Runas.md`, espelhando R0001–R0007 do Banco de Magias,
  com a diferença de acesso do `GET`.
- `Requisitos - Ficha de Personagem.md` 4.d: a criação parte do zero ou do banco; toda Runa vai ao banco.
  `Requisitos - Ficha de NPCs.md` herda por silêncio (nota curta).
- `Requisitos - Campanha.md`: R0006 (anexar entradas do Banco de Runas), R0008 (toggle) e R0012 (Runa criada
  por jogador vira anexo público).
- `Requisitos - Modelo de Dados.md`: `RuneBankEntry`, `SourceBankEntryId` em `CharacterRunes`/`NpcRunes`,
  `RuneBankEntryId` em `CampaignAttachments`.

## Testes (TDD)

- **Unit:** `CampaignAttachmentTargetValidator` com 6 alvos (exatamente um).
- **Integração:**
  - `RuneBankController`: CRUD, filtros por Nome/Grau, escopo por GM (404 em entrada alheia), `POST`/`PUT`/
    `DELETE`/`GET` proibidos a jogador (403).
  - Fichas (Personagem e NPC): adicionar do zero cria a cópia no banco; adicionar do banco copia os campos e
    grava `SourceBankEntryId`; exatamente-um-caminho (400 nos casos inválidos); jogador só usa entradas
    públicas; criação por jogador gera o anexo público; criação pelo GM não gera.
  - Campanha: anexar Runa, alvo duplicado/ausente → 400, toggle público/privado, `available-runes` só com as
    públicas, visão do jogador.
  - Migration: a tabela nova e as colunas.
- **Cliente:** formulário do banco, filtros e o seletor de origem de 4.d.

## Rollout

Migration nova → depois do `make deploy` é preciso `make migrate` (o stack local roda em Production e não
aplica migrations sozinho).
