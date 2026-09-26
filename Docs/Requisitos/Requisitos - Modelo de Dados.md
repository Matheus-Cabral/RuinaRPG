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
| IsRulesAuditor | bool — concedido/revogado via `make grant-rules-auditor`/`revoke-rules-auditor` (ver "[[Requisitos - Auditoria de Regras]]" R0001) |
| LastSeenAppVersion | string, nullable | última versão do app (ver `AppVersionInfo.Current` no código) cujo popup de changelog este usuário já fechou; null = nunca fechou nenhuma |
| MustChangePassword | bool — setado por `make reset-gm-password` (ver "[[Requisitos - Login e Cadastro]]" R0005); limpo quando o próprio usuário troca a senha |

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
| Subcategoria | string, nullable | todos os 5 tipos (coluna própria por subtipo, TPH) |
| Descricao | text, nullable | ItemGeral |
| CapacidadeExtra | decimal, nullable | ItemGeral — ver "[[Requisitos - Ficha de Personagem]]" 2.b/5.a e "[[Requisitos - Catálogo de Itens e Equipamentos]]" R0003 |
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

**RuneBankEntries** *(ver "[[Requisitos - Banco de Runas]]")* — biblioteca de Runas do GM; cada Runa criada numa ficha (Personagem ou NPC) tem uma cópia independente aqui.

| Coluna | Tipo | Nota |
|---|---|---|
| Id | PK | |
| GmId | FK → Users | |
| Nome | string | |
| Descricao | text | |
| Grau | int | |
| ImageId | FK → Images, nullable | imagem opcional da Runa; `SetNull` ao apagar a imagem |

  

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

**CampaignAttachments** — polimórfico via 6 FKs anuláveis (exatamente uma preenchida por linha).

| Coluna | Tipo | Nota |
|---|---|---|
| Id | PK | |
| CampaignId | FK → Campaigns | |
| ItemId | FK → Items, nullable | |
| NpcSheetId | FK → NpcSheets, nullable | |
| CreatureSheetId | FK → CreatureSheets, nullable | |
| SpellAbilityBankEntryId | FK → SpellAbilityBankEntries, nullable | |
| ImageId | FK → Images, nullable | imagem avulsa (R0007 da Campanha) |
| RuneBankEntryId | FK → RuneBankEntries, nullable | |
| IsPublic | bool | usado quando ItemId/SpellAbilityBankEntryId/RuneBankEntryId/ImageId está setado |
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
| HistoricoId | FK → Historicos, nullable | referência ao vivo (ver legenda) — nenhum campo é copiado para a ficha |
| Historia | text, nullable | História — HTML do texto rico, sanitizado no servidor por uma allowlist antes de gravar; NULL = vazio (ver "[[Requisitos - Ficha de Personagem]]", aba História) |
| Nivel | int | 1.b |
| Circulo | int | 1.b — colunas vestigiais: mantidas no schema mas não mais atualizadas pela aplicação; a Graduação exibida (`GraduacaoLabel`/`Graduacao` na response) é hoje **computada em tempo de leitura** a partir de `EAPAtual`/`Vocacao`, não lida daqui. Mesma situação em NpcSheets (6.2), que herda esta tabela sem diferença nesses dois campos. |
| Grau | int | 1.b — ver nota de `Circulo` acima. |
| PossuiCoracaoDeMana | bool | 1.b |
| ExperienciaAtual | int | 1.b |
| EAPAtual | int | 1.b |
| NucleosRankF..NucleosRankS | int × 7 | 1.b (Âmbares Absorvidos) |
| PontosDeIgnicaoAtual, PontosDeIgnicaoTotal | int | 1.b |
| VitalidadeAtual, FocoAtual, AdrenalinaAtual, EstresseAtual | int | 1.c |
| Cobertura | enum Nenhuma \| Parcial \| Completa | 2.b |
| Ciclos | int | 5.a |
| ArcaRolada | int, nullable | 4.a — só relevante quando `Linhagem` = Humano; resolvida contra `ArcaEntries` (seção 10). Mesma coluna existe em NpcSheets (6.2), sem diferença. |
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

**CharacterAffinities** — lista incremental (2.c). `SubElemento` é derivado pelo servidor a partir da Matriz Elemental (interseção de `Elemento` com `SegundaEssencia`) — ver Ficha de Personagem 2.c.

| Coluna | Tipo |
|---|---|
| Id | PK |
| CharacterSheetId | FK |
| Elemento | enum |
| SubElemento | enum |
| SegundaEssencia | enum EssenciaBasica (Ar, Água, Fogo, Terra, Alma, Vida, Mundano) — nullable |
| SegundaEssenciaValor | int — nullable |
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
| SourceBankEntryId | FK → RuneBankEntries, nullable (só rastreabilidade — R0003 do Banco de Runas) |
| ImageId | FK → Images, nullable (`SetNull`) — imagem opcional; cópia da imagem da entrada quando a Runa parte do banco |

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
| IsRacial | bool — concedida automaticamente pela Variante (5.d), custo 0, fora do orçamento |
| RacialVariante | enum Variante, nullable — qual Variante concedeu, null quando IsRacial é falso |

**Traits** — `Características.md` convertido em tabela (dado estático, seedado a partir do documento).

| Coluna | Tipo |
|---|---|
| Id | PK |
| Nome | string |
| Descricao | text |
| Custo | int |
| Polaridade | enum Positiva \| Negativa |
| IsCustomized | bool — true depois de criada/editada pelo Auditor de Regras; protege a linha de ser sobrescrita pelo re-seed a partir de Características.md |
| IsDeleted | bool — soft delete pelo Auditor de Regras; oculta a linha de toda leitura, mas ela continua existindo para o re-seed nunca recriá-la |
| UpdatedByUserId | FK → Users, nullable |
| UpdatedAt | DateTime, nullable |

  

**Historicos** — `Historico.md` convertido em tabela (dado estático, seedado a partir do documento).

| Coluna | Tipo |
|---|---|
| Id | PK |
| Nome | string |
| Descricao | text |
| PericiaMaisSeis | enum Pericia |
| PericiaMaisTres | enum Pericia |
| IsCustomized | bool — true depois de criada/editada pelo Auditor de Regras; protege a linha de ser sobrescrita pelo re-seed a partir de Historico.md |
| IsDeleted | bool — soft delete pelo Auditor de Regras; oculta a linha de toda leitura, mas ela continua existindo para o re-seed nunca recriá-la |
| UpdatedByUserId | FK → Users, nullable |
| UpdatedAt | DateTime, nullable |

**EquipmentKits**

Catálogo global (não por GM) de kits de equipamento inicial, cadastrado a partir de "[[Equipagem]]" e editável pelo Auditor de Regras (ver "[[Requisitos - Auditoria de Regras]]").

- `Id` (PK)
- `Nome` (string, obrigatório)
- `Descricao` (string, obrigatório — o parágrafo de sabor do kit)
- `Ciclos` (int — moeda concedida ao escolher o kit)
- `IsDeleted` (bool — soft delete)

**EquipmentKitItems**

Linhas fixas de um `EquipmentKit` — referenciam um Item do catálogo do GM por **Nome + Tipo**, nunca por Id (cada GM tem sua própria cópia do catálogo de Itens).

- `Id` (PK)
- `KitId` (FK → EquipmentKits, cascade)
- `Nome` (string — Nome do Item alvo no catálogo do GM)
- `Tipo` (enum ItemTipo — ItemGeral, Arma, Escudo ou Artefato; nunca Armadura)
- `Qtd` (int)
- `SubcategoriaHint` (string?, opcional — usado só quando o Item precisa ser criado automaticamente no catálogo do GM por não existir ainda)

**EquipmentKitChoiceSlots**

Linhas de escolha do jogador de um `EquipmentKit` (ex: "1 Arma Rank F de sua escolha"), resolvidas ao vivo contra o catálogo do GM no momento de aplicar o kit.

- `Id` (PK)
- `KitId` (FK → EquipmentKits, cascade)
- `Label` (string — ex: "Arma", "Condutor")
- `Tipo` (enum ItemTipo — Arma, Armadura, Escudo ou Artefato; nunca ItemGeral — ver "Novas colunas" abaixo para `ArmorSlot`, obrigatório quando `Tipo=Armadura`)
- `SubcategoriasCsv` (string?, opcional — lista de valores aceitos separados por vírgula; cada valor casa tanto com a Subcategoria legada em texto livre quanto com a Família parseada de um valor composto pelo construtor "Item Inicial" — ver "[[Requisitos - Catálogo de Itens e Equipamentos]]" R0013; NULL = qualquer uma)
- `Tier` (enum Tier?, opcional — NULL = qualquer Tier)
- `Qtd` (int)
- `BonusSubcategoria` (string?, opcional — Subcategoria do item escolhido que ativa um bônus condicional)
- `BonusNome` (string?, opcional — Item concedido além da escolha, só se `BonusSubcategoria` bater)
- `BonusQtd` (int?, opcional)

### SubcategoriaOptions

Catálogo global (não por GM) de valores de Categoria/Família usados pelo construtor de Subcategoria do cadastro de Item (ver "[[Requisitos - Catálogo de Itens e Equipamentos]]") e pelos slots de escolha da Equipagem — editável pelo Auditor de Regras na página de Auditoria de Equipagem.

- `Id` (PK)
- `Tipo` (enum ItemTipo — Arma, Armadura, Escudo ou Artefato; nunca ItemGeral)
- `Facet` (enum SubcategoriaFacet — Categoria ou Familia)
- `Valor` (string, obrigatório)
- `IsDeleted` (bool — soft delete)

### Novas colunas

- `CharacterSheets.EquipmentKitId` (Guid?, FK → EquipmentKits, SetNull) — NULL até o jogador escolher um kit; depois disso, permanente (não pode ser trocado).
- `NpcSheets.EquipmentKitId` (Guid?, FK → EquipmentKits, SetNull) — mesmo comportamento.
- `Items.Subcategoria` (string?, opcional) — já existia em Item Geral e Arma; passa a existir também em Armadura, Escudo e Artefato, com o mesmo comportamento (texto livre, ou o texto composto pelo construtor "Item Inicial" — ver "[[Requisitos - Catálogo de Itens e Equipamentos]]").
- `EquipmentKitChoiceSlots.ArmorSlot` (enum ArmorSlotType?, opcional) — obrigatório quando o slot é `Tipo=Armadura` (indica em qual slot de armadura da ficha — Capacete/Superior/Inferior — o item concedido é colocado); deve ficar vazio para os outros Tipos.

## 6.2 NpcSheets — diferenças de CharacterSheets

*(ver "[[Requisitos - Ficha de NPCs]]")*

Mesma família completa de tabelas filhas (`NpcAttributes`, `NpcSkills`, `NpcWeapons`, ... — um espelho 1:1 das tabelas de 6.1, prefixadas `Npc` em vez de `Character`), com estas diferenças na tabela raiz:

- `OwnerId`: **nullable** — só setado se concedida a um jogador (Campanha R0010).
- `CampaignId`: **não existe** aqui — o vínculo com campanha é via `CampaignAttachments` (seção 5), não uma FK direta.
- Ganha `NomePublico`/`ImagemPublica` **não** — esses toggles vivem em `CampaignAttachments`, não na ficha (podem diferir por campanha).
- `NpcRunes` ganha `SourceBankEntryId` (FK → RuneBankEntries, nullable) e `ImageId` (FK → Images, nullable, `SetNull`), como `CharacterRunes`.
- `HistoricoId`: FK → Historicos, nullable — referência ao vivo (ver legenda), mesmo comportamento de CharacterSheets.

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
- `CreatureTraits` diverge de `CharacterTraits`/`NpcTraits`: `TraitId` também **nullable**, e ganha `CreatureExclusiveTraitId` (FK → CreatureExclusiveTraits, nullable) — exatamente uma das duas é setada por linha, mesmo padrão de `EncounterParticipant.SourceCharacterSheetId`/`SourceNpcSheetId`/`SourceCreatureSheetId` (seção 8): dois FKs nullable em vez de um só, porque uma coluna não carrega FK real pra duas tabelas diferentes ao mesmo tempo.

**CreatureExclusiveTraits** — catálogo global e independente de `Traits` (6.1), com as mesmas colunas exceto `IsCustomized`: sem documento-fonte equivalente a `Características.md` pra essa tabela, então não há re-seed do qual proteger uma linha editada manualmente. Só o picker de característica da Ficha de Criatura (`CreatureTraits`) referencia esta tabela — Personagem, NPC e o Compêndio de Regras nunca a leem.

| Coluna | Tipo |
|---|---|
| Id | PK |
| Nome | string |
| Descricao | text |
| Custo | int |
| Polaridade | enum Positiva \| Negativa |
| RequerEspecificacao | bool |
| IsDeleted | bool — soft delete pelo Auditor de Regras, mesma lógica de `Traits.IsDeleted` |
| UpdatedByUserId | FK → Users, nullable |
| UpdatedAt | DateTime, nullable |

  

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
| SourceCharacterSheetId | FK → CharacterSheets, nullable | no máximo uma das três colunas `Source*SheetId` é setada (R0002) |
| SourceNpcSheetId | FK → NpcSheets, nullable | idem |
| SourceCreatureSheetId | FK → CreatureSheets, nullable | idem |
| Nome | string | snapshot, pra exibição mesmo se a origem for excluída depois |
| Iniciativa | int | digitada manualmente pelo GM (R0003) |
| PVAtual, PFAtual, PAAtual | int, nullable | ver regra abaixo |
| AcoesRestantes | int | |

A regra de PV/PF/PA não depende só de qual `Source*SheetId` está setada, mas também de `OwnerId` da ficha de origem (NpcSheets/CreatureSheets):

- **Live-link a outra ficha** (colunas ficam nulas, valor lido ao vivo da origem): `SourceCharacterSheetId` setado, OU `SourceNpcSheetId`/`SourceCreatureSheetId` setada apontando pra uma ficha **concedida** (`OwnerId` não nulo — pet/invocação de jogador).
- **Cópia independente** (colunas copiadas da origem no momento da criação do participante e depois editáveis por conta própria): `SourceNpcSheetId`/`SourceCreatureSheetId` setada apontando pra uma ficha do **bestiário do próprio GM** (`OwnerId` nulo).

**EncounterParticipantConditions** — tags de condição (texto livre, R0003).

| Coluna | Tipo |
|---|---|
| Id | PK |
| EncounterParticipantId | FK |
| Texto | string |

  

# 9. Compêndio de Regras

*(ver "[[Requisitos - Compêndio de Regras]]")*

Sem tabelas próprias — o conteúdo é estático e vem direto de `Docs/Sistema RPG/`. A indexação para busca (R0001–R0003 daquele documento) é construída em memória/build-time a partir dos arquivos-fonte, não persistida como dado de aplicação. Exceção: **Traits** (seção 6.1) já existe como tabela porque é referenciada por FK de `CharacterTraits` — o Compêndio busca nela em vez de duplicar.

  

# 10. Habilidades Raciais

*(ver "[[Requisitos - Habilidades Raciais]]")*

**RacialAbilityOverrides** — sobrescrita do GM pro Nome/Descrição hardcoded de uma Variante (R0001); o padrão vale quando a linha não existe. Uma linha por (GM, Variante).

| Coluna | Tipo |
|---|---|
| Id | PK |
| GmId | FK → Users |
| Variante | enum (mesmo enum de 6.1, 8 valores, incluindo a variante solar de Alóra) |
| Nome | string |
| Descricao | text |
| NomeDaVariante | string, nullable — só usado pela variante solar de Alóra: o nome dado pelo GM, que a libera nas fichas quando preenchido (R0005) |

**RacialTraitOverrides** — sobrescrita do GM pras opções de Característica Gratuita/Obrigatória de uma Variante (R0004); o padrão de "[[Ruína RPG - Sistema Básico]]" §7 vale quando a linha não existe. Uma linha por (GM, Variante); cada lista de opções é um JSON (array de {TraitNome, Especificacao}), não linhas filhas — tamanho pequeno e fixo, nunca consultado pelo conteúdo.

| Coluna | Tipo |
|---|---|
| Id | PK |
| GmId | FK → Users |
| Variante | enum (mesmo enum de 6.1, 8 valores) |
| GratuitaOptionsJson | text (JSON) |
| ObrigatoriaOptionsJson | text (JSON) |

**ArcaEntries** — a "tabela de Arcas" (1d18) referenciada pelo Racial de Sinir/Laonir, conteúdo livre do GM (R0002). Uma linha por (GM, Roll de 1 a 18) já preenchido; ausência de linha para um Roll = "não cadastrada".

| Coluna | Tipo |
|---|---|
| Id | PK |
| GmId | FK → Users |
| Roll | int, 1 a 18 |
| Nome | string |
| Descricao | text |

  

# 11. Auditoria de Regras

*(ver "[[Requisitos - Auditoria de Regras]]")*

**RulebookDocumentOverrides** — sobrescrita do texto Markdown de um dos 3 documentos do Livro de Regras que não são a aba de Características (R0002); o padrão embutido no build vale quando a linha não existe. Uma linha por Slug (não por GM — vale pro servidor inteiro).

| Coluna | Tipo |
|---|---|
| Id | PK |
| Slug | string, único — "sistema-basico" \| "graus-e-circulos" \| "tabela-de-niveis" |
| MarkdownText | text |
| UpdatedByUserId | FK → Users |
| UpdatedAt | DateTime |
