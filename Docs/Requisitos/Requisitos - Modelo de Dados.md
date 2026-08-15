> Este documento não é um requisito funcional — é o desenho do schema relacional (Postgres, via EF Core) que implementa os documentos de `Docs/Requisitos/`. Ele registra **estrutura** (entidades, colunas, relacionamentos); a semântica de cada campo (validação, fórmula, quem pode editar) já está descrita no documento de Requisitos correspondente e não é repetida aqui — cada seção linka de volta pra lá.

  

> **Convenções**: `PK` = chave primária (Guid). `FK → X` = chave estrangeira pra tabela X. Enums são referenciados pelo nome sem listar os valores quando já enumerados em outro documento (evita duplicar dado de jogo — ver o link).

  

> **Três padrões de vínculo recorrentes**, usados o tempo todo abaixo:

> - **Cópia independente**: os campos são copiados no momento da criação/seleção; a linha vive por conta própria depois disso (ex: Magia/Habilidade escolhida do Banco, Ficha de NPC/Criatura concedida "cópia de uma existente").
> - **Referência ao vivo ao Catálogo**: uma FK pro item do Catálogo; os campos exibidos vêm sempre do registro atual do Catálogo, somente leitura (ex: Arma/Armadura/Escudo/Artefato equipados na Ficha de Personagem). **Exceção**: Durabilidade Atual (Arma/Armadura/Escudo) não é ao vivo — é um valor por instância, inicializado a partir do Máximo do Catálogo e depois independente.
> - **Live-link a outra ficha**: nenhuma cópia de dado — só uma FK; o valor exibido é lido direto da ficha de origem em tempo real (ex: PV/PF/PA de um participante de Encontro vindo de Ficha de Personagem).

  

# 1. Contas e Convites

*(ver "[[Requisitos - Convite de Jogador]]")*

**Users** — estende o `AspNetUsers` do Identity.

| Coluna | Tipo | Nota |
|---|---|---|
| Id | PK | |
| Nickname | string | |
| Role | enum GM \| Jogador | |
| InvitedByGmId | FK → Users, nullable | só em usuários Jogador; setado ao resgatar um InviteCode |

**InviteCodes**

| Coluna | Tipo | Nota |
|---|---|---|
| Id | PK | |
| Code | string(8), unique | |
| GmId | FK → Users | |
| GeneratedAt | datetime | |
| ExpiresAt | datetime | `GeneratedAt + 48h` (R0001) |
| RevokedAt | datetime, nullable | |
| RedeemedByUserId | FK → Users, nullable | |
| RedeemedAt | datetime, nullable | |

Status (Ativo/Usado/Revogado/Expirado, R0002) é **computado**, não armazenado: deriva de `RevokedAt`, `RedeemedByUserId` e `ExpiresAt` vs. agora.

**RefreshTokens** — não descrita em nenhum documento de Requisitos funcional, mas exigida pela stack já decidida (ver "[[Requisitos - Técnico]]" R0001/R0004: JWT + refresh tokens).

| Coluna | Tipo | Nota |
|---|---|---|
| Id | PK | |
| UserId | FK → Users | |
| TokenHash | string | nunca o token em texto puro |
| ExpiresAt | datetime | ver `Jwt__RefreshTokenDays` |
| RevokedAt | datetime, nullable | |

  

# 2. Imagens

*(ver "[[Requisitos - Catálogo de Itens e Equipamentos]]" R0010)*

**Images** — entidade compartilhada; toda imagem do sistema é uma linha aqui, referenciada por FK de onde for usada. Isso é o que viabiliza "referenciar em vez de reenviar": duas fichas/entradas podem apontar pro mesmo `ImageId`.

| Coluna | Tipo | Nota |
|---|---|---|
| Id | PK | |
| Path | string | caminho no volume de armazenamento |
| ContentType | string | |
| UploadedByUserId | FK → Users | |
| CreatedAt | datetime | |

  

# 3. Catálogo de Itens e Equipamentos

*(ver "[[Requisitos - Catálogo de Itens e Equipamentos]]")*

**Items** — Table-Per-Hierarchy: uma tabela, discriminada por `Tipo`.

| Coluna | Tipo | Aplica-se a |
|---|---|---|
| Id | PK | todos |
| GmId | FK → Users | todos |
| Tipo | enum ItemGeral \| Arma \| Armadura \| Escudo \| Artefato | todos (discriminador) |
| Nome | string | todos |
| Peso | decimal | todos |
| Preco | int | todos (Ciclos, R0008) |
| ImageId | FK → Images, nullable | todos |
| Subcategoria | string, nullable | ItemGeral, Arma |
| Descricao | text, nullable | ItemGeral |
| Tier | enum F..S, nullable | Arma |
| Empunhadura | enum, nullable | Arma |
| Dados | string, nullable | Arma |
| Dano | int, nullable | Arma |
| Critico | string, nullable | Arma |
| Alcance | int, nullable | Arma |
| TipoDeDano | enum, nullable | Arma |
| RequisitoAtributo | string, nullable | Arma |
| Categoria | enum Leve \| Medio \| Pesada, nullable | Armadura, Escudo |
| Defesa | int, nullable | Armadura |
| RF | int, nullable | Armadura |
| RM | int, nullable | Armadura |
| BonusDefesa | int, nullable | Escudo |
| Penalidade | string, nullable | Armadura, Escudo |
| RequisitoVigor | int, nullable | Armadura, Escudo |
| TipoDeAlvo | enum Atributo \| Pericia \| SubAtributo \| Dano, nullable | Artefato |
| Alvo | string, nullable | Artefato |
| Valor | int, nullable | Artefato |
| DurabilidadeMaxima | int, nullable | Arma, Armadura, Escudo — valor de referência definido pelo GM; o valor **atual** não mora aqui, mora por instância (ver `CharacterWeapons`/`CharacterArmorSlots`/`CharacterShields` na seção 6) |

  

# 4. Banco de Magias e Habilidades

*(ver "[[Requisitos - Banco de Magias e Habilidades]]")*

**SpellAbilityBankEntries**

| Coluna | Tipo | Nota |
|---|---|---|
| Id | PK | |
| GmId | FK → Users | |
| Nome | string | |
| Tipo | enum Magia \| Habilidade \| Racial | |
| Grau | int | |
| GastoEmPI | int | soma dos Efeitos (abaixo) |
| Custo | int | `teto(1,25 × GastoEmPI)` |
| Descricao | text | |

**SpellAbilityBankEffects** — os Efeitos comprados de uma entrada.

| Coluna | Tipo | Nota |
|---|---|---|
| Id | PK | |
| SpellAbilityBankEntryId | FK → SpellAbilityBankEntries | |
| EfeitoNome | string | ex: "Dano", "Alcance", "Aumentar Armadura" |
| Quantidade | int, nullable | pra efeitos medidos em unidades (Dano/Alcance/Duração) |
| CustoPI | int | |

O mesmo par de tabelas (entrada + efeitos) se repete, como **cópia independente**, em `CharacterSpellAbilities`/`NpcSpellAbilities`/`CreatureSpellAbilities` (seção 6) — modelar como um tipo owned/complexo do EF Core reaproveitado nas 4 tabelas é razoável, já que o formato é idêntico.

  

# 5. Campanhas

*(ver "[[Requisitos - Campanha]]")*

**Campaigns**

| Coluna | Tipo |
|---|---|
| Id | PK |
| GmId | FK → Users |
| Nome | string |
| Descricao | text |

**CampaignMembers**

| Coluna | Tipo |
|---|---|
| Id | PK |
| CampaignId | FK → Campaigns |
| UserId | FK → Users |

**CampaignAttachments** — polimórfico via 5 FKs anuláveis (exatamente uma preenchida por linha).

| Coluna | Tipo | Nota |
|---|---|---|
| Id | PK | |
| CampaignId | FK → Campaigns | |
| ItemId | FK → Items, nullable | |
| NpcSheetId | FK → NpcSheets, nullable | |
| CreatureSheetId | FK → CreatureSheets, nullable | |
| SpellAbilityBankEntryId | FK → SpellAbilityBankEntries, nullable | |
| ImageId | FK → Images, nullable | imagem avulsa (R0007 da Campanha) |
| IsPublic | bool | usado quando ItemId/SpellAbilityBankEntryId/ImageId está setado |
| NpcNomePublico | bool | usado quando NpcSheetId está setado (R0004 do NPC) |
| NpcImagemPublica | bool | idem |
| CreatureNomePublico | bool | usado quando CreatureSheetId está setado (R0003 da Criatura) |
| CreatureImagemPublica | bool | idem |

  

# 6. Fichas

*(ver "[[Requisitos - Ficha de Personagem]]", "[[Requisitos - Ficha de NPCs]]", "[[Requisitos - Ficha de Criaturas]]")*

Cada tipo de ficha (Personagem, NPC, Criatura) é sua própria família de tabelas — a diferença real de campos entre elas (documentada como diffs) não cabe bem numa hierarquia TPH única. As três famílias replicam a mesma forma geral; as colunas abaixo cobrem a família **CharacterSheet** (Personagem) por extenso, e as duas seções seguintes só listam as **diferenças** de NPC e Criatura, no mesmo espírito dos próprios documentos de Requisitos.

## 6.1 CharacterSheets (Personagem)

| Coluna | Tipo | Aba (ver Ficha de Personagem) |
|---|---|---|
| Id | PK | |
| CampaignId | FK → Campaigns | criada dentro de uma campanha (Campanha R0004) |
| OwnerId | FK → Users | |
| ImageId | FK → Images, nullable | 1.a |
| Nome | string | 1.a |
| Linhagem | enum, nullable | 1.a |
| Variante | enum, nullable | 1.a |
| Vocacao | enum, nullable | 1.a |
| SubVocacao | string, nullable | 1.a |
| Afinidade | enum (Elemento \| Sub-Elemento), nullable | 1.a |
| Propriedade | string, nullable | 1.a |
| Nivel | int | 1.b |
| Circulo | int | 1.b |
| Grau | int | 1.b |
| PossuiCoracaoDeMana | bool | 1.b |
| ExperienciaAtual | int | 1.b |
| EAPAtual | int | 1.b |
| NucleosRankF..NucleosRankS | int × 7 | 1.b (Âmbares Absorvidos) |
| PontosDeIgnicaoAtual, PontosDeIgnicaoTotal | int | 1.b |
| VitalidadeAtual, FocoAtual, AdrenalinaAtual, EstresseAtual | int | 1.c |
| Cobertura | enum Nenhuma \| Parcial \| Completa | 2.b |
| Ciclos | int | 5.a |
| LastDismissedLevelUpLevel | int, nullable | R0002 — até qual Nível a caixa de aviso já foi fechada |

**CharacterAttributes** — 1 linha por atributo (8 por ficha).

| Coluna | Tipo |
|---|---|
| Id | PK |
| CharacterSheetId | FK |
| Atributo | enum (2.a) |
| Gasto | int |
| Bonus | int |
| TemMaestria | bool |

**CharacterSkills** — 1 linha por Perícia (2.d).

| Coluna | Tipo |
|---|---|
| Id | PK |
| CharacterSheetId | FK |
| Pericia | enum |
| Gasto | int |

*(Modificador e Total não são colunas — são calculados; Atributo usado no teste é escolhido no momento da rolagem, não persistido.)*

**CharacterAffinities** — lista incremental (2.c).

| Coluna | Tipo |
|---|---|
| Id | PK |
| CharacterSheetId | FK |
| Elemento | enum |
| SubElemento | enum |
| CaminhoNome | string |
| Experiencia | int |

**CharacterWeapons** — arsenal (3.a). Referência ao vivo ao Catálogo, exceto Durabilidade.

| Coluna | Tipo |
|---|---|
| Id | PK |
| CharacterSheetId | FK |
| ItemId | FK → Items (Tipo=Arma) |
| IsEquipped | bool |
| DurabilidadeAtual | int | inicializada = `Items.DurabilidadeMaxima` no momento em que a linha é criada; editável e independente depois |

**CharacterArmorSlots** — 3 linhas fixas por ficha (3.b). Referência ao vivo ao Catálogo, exceto Durabilidade.

| Coluna | Tipo |
|---|---|
| Id | PK |
| CharacterSheetId | FK |
| Slot | enum Capacete \| Superior \| Inferior |
| ItemId | FK → Items (Tipo=Armadura), nullable |
| DurabilidadeAtual | int, nullable | nulo enquanto o slot está vazio; mesma inicialização de `CharacterWeapons` |

**CharacterShields** — arsenal (3.c). Referência ao vivo ao Catálogo, exceto Durabilidade.

| Coluna | Tipo |
|---|---|
| Id | PK |
| CharacterSheetId | FK |
| ItemId | FK → Items (Tipo=Escudo) |
| IsEquipped | bool |
| DurabilidadeAtual | int | mesma inicialização de `CharacterWeapons` |

**CharacterSpellAbilities** — cópia independente (4.b).

| Coluna | Tipo |
|---|---|
| Id | PK |
| CharacterSheetId | FK |
| SourceBankEntryId | FK → SpellAbilityBankEntries, nullable (só rastreabilidade — R0003 do Banco) |
| Nome | string |
| Tipo | enum Magia \| Habilidade |
| Grau | int |
| GastoEmPI | int |
| Custo | int |
| Descricao | text |

**CharacterSpellAbilityEffects** — mesma forma de `SpellAbilityBankEffects`, FK pra `CharacterSpellAbilities`.

**CharacterRunes** (4.d)

| Coluna | Tipo |
|---|---|
| Id | PK |
| CharacterSheetId | FK |
| Nome | string |
| Descricao | text |
| Grau | int |

**CharacterMasteries** (4.e)

| Coluna | Tipo |
|---|---|
| Id | PK |
| CharacterSheetId | FK |
| Nome | string |
| Pericia | enum |
| Atributo | enum |
| GastoMaestria | int |

**CharacterInventoryItems** (5.a) — referência ao vivo ao Catálogo.

| Coluna | Tipo |
|---|---|
| Id | PK |
| CharacterSheetId | FK |
| ItemId | FK → Items (Tipo=ItemGeral) |
| Qtd | int |

**CharacterArtifacts** (5.b) — referência ao vivo ao Catálogo; limite de 3 por `TipoDeAlvo` validado na aplicação, não no schema.

| Coluna | Tipo |
|---|---|
| Id | PK |
| CharacterSheetId | FK |
| ArtifactItemId | FK → Items (Tipo=Artefato) |

**CharacterAffections** (5.c)

| Coluna | Tipo |
|---|---|
| Id | PK |
| CharacterSheetId | FK |
| Nome | string |
| Favorabilidade | int |

**CharacterTraits** (5.d)

| Coluna | Tipo |
|---|---|
| Id | PK |
| CharacterSheetId | FK |
| TraitId | FK → Traits |
| Polaridade | enum Positiva \| Negativa |

**Traits** — `Características.md` convertido em tabela (dado estático, seedado a partir do documento).

| Coluna | Tipo |
|---|---|
| Id | PK |
| Nome | string |
| Descricao | text |
| Custo | int |
| Polaridade | enum Positiva \| Negativa |

## 6.2 NpcSheets — diferenças de CharacterSheets

*(ver "[[Requisitos - Ficha de NPCs]]")*

Mesma família completa de tabelas filhas (`NpcAttributes`, `NpcSkills`, `NpcWeapons`, ... — um espelho 1:1 das tabelas de 6.1, prefixadas `Npc` em vez de `Character`), com estas diferenças na tabela raiz:

- `OwnerId`: **nullable** — só setado se concedida a um jogador (Campanha R0010).
- `CampaignId`: **não existe** aqui — o vínculo com campanha é via `CampaignAttachments` (seção 5), não uma FK direta.
- Ganha `NomePublico`/`ImagemPublica` **não** — esses toggles vivem em `CampaignAttachments`, não na ficha (podem diferir por campanha).

## 6.3 CreatureSheets — diferenças de CharacterSheets

*(ver "[[Requisitos - Ficha de Criaturas]]")*

Mesma lógica de 6.2: família completa de tabelas filhas espelhando 6.1 (prefixadas `Creature`), exceto onde dito abaixo. `CreatureSpellAbilities`, `CreatureMasteries` e `CreatureArtifacts` existem sem alteração de forma.

- `OwnerId`: nullable, mesma regra de NpcSheets.
- `CampaignId`: não existe, mesma regra de NpcSheets.
- Troca `Linhagem`/`Variante`/`Vocacao`/`SubVocacao` por: `Raca` (string), `Arquetipo` (enum Fisico \| Arcano), `SubArquetipo` (string).
- Troca `Circulo`/`Grau`/`EAPAtual`/`NucleosRank*` por: `Rank` (enum F..S).
- `PontosDeIgnicao`: **um único int**, não par atual/total.
- Sem `EstresseAtual`.
- `CreatureAttributes.Atributo` usa um enum próprio de 6 valores (Força, Vigor, Agilidade, Destreza, Astúcia, **Ego**), não o de 8 valores do Personagem.
- `CreatureSkills.Pericia` só permite o subconjunto ~20 de Perícias listado em R0005 (restrição de aplicação, não de schema, já que reaproveita o enum `Pericia` completo).
- Sem `CharacterAffinities` equivalente (não existe aba de Afinidades pra Criatura).
- `CreatureWeapons`: `ItemId` **nullable** — quando nulo, usa `ManualNome`/`ManualTipoDeDano`/`ManualDados`/`ManualDano` (ataque natural, sem Alcance/Crítico/Tier/Durabilidade); `DurabilidadeAtual` também fica nula nesse caso.
- Sem `CreatureRunes` nem tabela de Contratos (não existem pra Criatura).
- `CreatureInventoryItems` → renomeada `CreatureSpoils` (Espólios), ganha coluna `DT` (int) e **perde** `Ciclos` na ficha raiz.
- Ganha `ExperienciaAtual` própria (já existe, herdada da estrutura de Nível) usada para computar **Kill** = `piso(ExperienciaAtual × 0,15)` e **Assistência** = `piso(ExperienciaAtual × 0,12)` — calculados, não persistidos.

  

# 7. Diário e Notas Secretas

*(ver "[[Requisitos - Ficha de Personagem]]" 6, "[[Requisitos - Campanha]]" R0005/R0011)*

Uma única família de tabelas cobre os três casos (diário do Personagem, diário da Campanha, Notas Secretas), diferenciados por qual FK está preenchida e pela flag `IsSecretNote`.

**DiaryEntries**

| Coluna | Tipo | Nota |
|---|---|---|
| Id | PK | |
| AuthorUserId | FK → Users | |
| CharacterSheetId | FK → CharacterSheets, nullable | diário do Personagem |
| CampaignId | FK → Campaigns, nullable | diário da Campanha ou Nota Secreta |
| IsSecretNote | bool | só relevante quando `CampaignId` setado |
| Texto | text | |
| CreatedAt | datetime | |

**DiaryEntryImages** (join — imagens podem ser upload novo ou referência a uma `Image` existente, ambos os casos só criam uma linha aqui apontando pro `ImageId` correspondente)

| Coluna | Tipo |
|---|---|
| DiaryEntryId | FK |
| ImageId | FK → Images |

**DiaryEntryRecipients** (join — só populada quando `IsSecretNote = true`)

| Coluna | Tipo |
|---|---|
| DiaryEntryId | FK |
| UserId | FK → Users |

  

# 8. Gerenciador de Encontros

*(ver "[[Requisitos - Gerenciador de Encontros]]")*

**Encounters**

| Coluna | Tipo |
|---|---|
| Id | PK |
| CampaignId | FK → Campaigns |
| Nome | string, nullable |
| CurrentRound | int |
| CurrentParticipantIndex | int |

**EncounterParticipants**

| Coluna | Tipo | Nota |
|---|---|---|
| Id | PK | |
| EncounterId | FK | |
| SourceCharacterSheetId | FK → CharacterSheets, nullable | setado quando vem de Personagem/pet-invocação (R0003) — PV/PF/PA lidos ao vivo de lá, não das colunas abaixo |
| Nome | string | snapshot, pra exibição mesmo se a origem for excluída depois |
| Iniciativa | int | digitada manualmente pelo GM (R0003) |
| PVAtual, PFAtual, PAAtual | int, nullable | só usados quando `SourceCharacterSheetId` é nulo (instância independente de NPC/Criatura) |
| AcoesRestantes | int | |

**EncounterParticipantConditions** — tags de condição (texto livre, R0003).

| Coluna | Tipo |
|---|---|
| Id | PK |
| EncounterParticipantId | FK |
| Texto | string |

  

# 9. Compêndio de Regras

*(ver "[[Requisitos - Compêndio de Regras]]")*

Sem tabelas próprias — o conteúdo é estático e vem direto de `Docs/Sistema RPG/`. A indexação para busca (R0001–R0003 daquele documento) é construída em memória/build-time a partir dos arquivos-fonte, não persistida como dado de aplicação. Exceção: **Traits** (seção 6.1) já existe como tabela porque é referenciada por FK de `CharacterTraits` — o Compêndio busca nela em vez de duplicar.
