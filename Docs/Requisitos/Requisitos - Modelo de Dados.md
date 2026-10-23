> Este documento não é um requisito funcional — é o desenho do schema relacional (Postgres, via EF Core) que implementa os documentos de `Docs/Requisitos/`. Ele registra **estrutura** (tabelas, colunas, relacionamentos); a semântica de cada campo (validação, fórmula, quem pode editar) já está descrita no documento de Requisitos correspondente e não é repetida aqui — cada seção linka de volta pra lá. Os nomes são os das **colunas** do banco; a referência é o snapshot do modelo EF Core (`RuinaRpgDbContextModelSnapshot.cs`).

  

> **Convenções**: `PK` = chave primária (Guid, salvo indicação). `FK → X` = chave estrangeira pra tabela X; `Users` = a tabela `AspNetUsers` do Identity. Colunas sem "nullable" são NOT NULL. Enums são guardados como inteiro, salvo quando a nota diz "texto", e são referenciados pelo nome sem listar os valores quando já enumerados em outro documento (evita duplicar dado de jogo — ver o link). "Soft delete" = coluna `IsDeleted`: a linha some de toda leitura mas continua existindo.

  

> **Três padrões de vínculo recorrentes**, usados o tempo todo abaixo:

> - **Cópia independente**: os campos são copiados no momento da criação/seleção; a linha vive por conta própria depois disso (ex: Magia/Habilidade escolhida do Banco, Ficha de NPC/Criatura concedida "cópia de uma existente").
> - **Referência ao vivo ao Catálogo**: uma FK pro item do Catálogo; os campos exibidos vêm sempre do registro atual do Catálogo, somente leitura (ex: Arma/Armadura/Escudo/Artefato equipados na Ficha de Personagem). **Exceção**: Durabilidade Atual (Arma/Armadura/Escudo) não é ao vivo — é um valor por instância, inicializado a partir do Máximo do Catálogo e depois independente.
> - **Live-link a outra ficha**: nenhuma cópia de dado — só uma FK; o valor exibido é lido direto da ficha de origem em tempo real (ex: PV/PF/PA de um participante de Encontro vindo de Ficha de Personagem).

  

# 1. Contas e Convites

*(ver "[[Requisitos - Convite de Jogador]]")*

**Users** (`AspNetUsers`) — a tabela de usuários do ASP.NET Core Identity, com as colunas abaixo acrescentadas às do Identity. As demais tabelas do Identity (`AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetRoleClaims`, `AspNetUserLogins`, `AspNetUserTokens`) existem sem alteração; o papel GM/Jogador é a coluna `Role`, não um papel do Identity.

| Coluna | Tipo | Nota |
|---|---|---|
| Id | PK | |
| Nickname | string | |
| NormalizedNickname | string, único | o Nickname normalizado; garante que "Mestre" e "mestre" não coexistam e é o que a busca por apelido compara |
| Role | enum GM \| Jogador | |
| InvitedByGmId | Guid, nullable | só em usuários Jogador; setado ao resgatar um InviteCode. Sem FK |
| IsRulesAuditor | bool | concedido/revogado via `make grant-rules-auditor`/`revoke-rules-auditor` (ver "[[Requisitos - Auditoria de Regras]]" R0001) |
| LastSeenAppVersion | string, nullable | última versão do app cujo popup de changelog este usuário já fechou; null = nunca fechou nenhuma |
| MustChangePassword | bool | setado por `make reset-gm-password` (ver "[[Requisitos - Login e Cadastro]]" R0005); limpo quando o próprio usuário troca a senha |

**InviteCodes**

| Coluna | Tipo | Nota |
|---|---|---|
| Id | PK | |
| Code | string(8), único | |
| GmId | FK → Users | |
| GeneratedAt | datetime | |
| ExpiresAt | datetime | `GeneratedAt + 48h` (R0001) |
| RevokedAt | datetime, nullable | |
| RedeemedByUserId | FK → Users, nullable (`SetNull`) | |
| RedeemedAt | datetime, nullable | |

Status (Ativo/Usado/Revogado/Expirado, R0002) é **computado**, não armazenado: deriva de `RevokedAt`, `RedeemedByUserId` e `ExpiresAt` vs. agora.

**RefreshTokens** — não descrita em nenhum documento de Requisitos funcional, mas exigida pela stack (ver "[[Requisitos - Técnico]]" R0001/R0004: JWT + refresh tokens).

| Coluna | Tipo | Nota |
|---|---|---|
| Id | PK | |
| UserId | FK → Users | |
| TokenHash | string, único | nunca o token em texto puro |
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

**Items** — Table-Per-Hierarchy: uma tabela, discriminada por `Tipo`. Rank e Subcategoria têm uma coluna própria por subtipo (`Arma_Rank`, `Arma_Subcategoria`, …), pois cada subtipo os mapeia separadamente; a Categoria da Armadura também (`Armadura_Categoria`), enquanto a do Escudo fica na coluna `Categoria`.

| Coluna | Tipo | Aplica-se a |
|---|---|---|
| Id | PK | todos |
| GmId | FK → Users | todos |
| Tipo | string(13) — `ItemGeral` \| `Arma` \| `Armadura` \| `Escudo` \| `Artefato` | todos (discriminador, texto) |
| Nome | string | todos |
| Peso | decimal | todos |
| Preco | int | todos (Ciclos, R0008) |
| ImageId | FK → Images, nullable (`SetNull`) | todos |
| Descricao | text, nullable | todos |
| ItemGeral_Subcategoria, Arma_Subcategoria, Armadura_Subcategoria, Escudo_Subcategoria, Artefato_Subcategoria | string, nullable | a coluna do próprio subtipo — texto livre ou o texto composto pelo construtor "Item Inicial" |
| CapacidadeExtra | decimal, nullable | ItemGeral — ver "[[Requisitos - Ficha de Personagem]]" 2.b/5.a e "[[Requisitos - Catálogo de Itens e Equipamentos]]" R0003 |
| Arma_Rank, Armadura_Rank, Escudo_Rank | enum RankDeItem (F, E, D, C, B, A, S, SS), nullable | a coluna do próprio subtipo; determina a Durabilidade Máxima via `DurabilidadesPorRank` (abaixo), NULL = sem durabilidade |
| Empunhadura | enum, nullable | Arma |
| Dados | string, nullable | Arma |
| Dano | int, nullable | Arma |
| Critico | string, nullable | Arma |
| Alcance | int, nullable | Arma |
| TipoDeDano | enum, nullable | Arma |
| Armadura_Categoria | enum Leve \| Medio \| Pesada, nullable | Armadura |
| Categoria | enum Leve \| Medio \| Pesada, nullable | Escudo |
| Defesa | int, nullable | Armadura |
| RF | int, nullable | Armadura |
| RM | int, nullable | Armadura |
| BonusDefesa | int, nullable | Escudo |
| Requisitos | jsonb, nullable | Arma, Armadura, Escudo, Artefato |
| PenalidadeDeRequisitos | jsonb, nullable | Arma, Armadura, Escudo, Artefato |
| TipoDeAlvo | enum Atributo \| Pericia \| SubAtributo \| Dano, nullable | Artefato |
| Alvo | string, nullable (para TipoDeAlvo = Pericia, é a `Chave` de `Pericias`) | Artefato |
| Valor | int, nullable | Artefato |

`Requisitos` guarda o que a ficha precisa ter para usar o item sem penalidade (Vocação, Classe, Atributo, Perícia, Sub-Atributo, Afinidade, Estrela); `PenalidadeDeRequisitos` guarda a penalidade aplicada enquanto eles não são cumpridos (linhas sobre Atributo/Perícia/Sub-Atributo mais o texto "Outras penalidades"). Ambos são NULL em item sem "Possui requisitos" — ver "[[Requisitos - Catálogo de Itens e Equipamentos]]" R0014.

**DurabilidadesPorRank** — "[[Tabela de Durabilidade por Rank]]" convertida em tabela (dado estático, seedado a partir do documento), mesmo tratamento de `Historicos`: o seed só insere as linhas que faltam, nunca sobrescreve uma linha editada pelo Auditor de Regras.

| Coluna | Tipo |
|---|---|
| Rank | enum RankDeItem (F, E, D, C, B, A, S, SS) — PK |
| Durabilidade | int, nullable |
| Inquebravel | bool — quando true, Durabilidade é NULL e o item não tem máximo (ver "[[Requisitos - Auditoria de Regras]]") |

**TabelaDeAfinidades** — "[[Tabela de Afinidades]]" convertida em tabela: Eficiência e Dano Elemental por valor de Afinidade (ver "[[Requisitos - Ficha de Personagem]]" 2.b). Semeada a partir do documento **só enquanto está vazia**; depois é mantida pelo Auditor de Regras ("[[Requisitos - Auditoria de Regras]]" R0016).

| Coluna | Tipo |
|---|---|
| Id | PK |
| Afinidade | int, único |
| Eficiencia | int, >= 0 |
| Dano | int, >= 0 |

  

# 4. Banco de Magias e Habilidades

*(ver "[[Requisitos - Banco de Magias e Habilidades]]")*

**SpellAbilityBankEntries**

| Coluna | Tipo | Nota |
|---|---|---|
| Id | PK | |
| GmId | FK → Users | |
| Nome | string | |
| Tipo | enum Magia \| Habilidade \| Racial \| Passiva | |
| Grau | int | |
| GastoEmPI | int | soma dos Efeitos (abaixo) |
| Custo | int | `teto(1,25 × GastoEmPI)` |
| Descricao | text | |
| DeCriatura | bool, default false | R0008 do Banco — só filtro; `true` automático na cópia vinda de uma Ficha de Criatura |
| Categoria | enum Livre \| Vocacional \| DeClasse, nullable | só em Passiva (Tipo = Passiva) |
| Requisitos | jsonb, nullable | só em Passiva; ver "[[Requisitos - Banco de Magias e Habilidades]]" R0009 |

**SpellAbilityBankEffects** — os Efeitos comprados de uma entrada.

| Coluna | Tipo | Nota |
|---|---|---|
| Id | PK | |
| SpellAbilityBankEntryId | FK → SpellAbilityBankEntries | |
| EfeitoNome | string | o `Nome` de uma linha de `Efeitos` (seção 11), sem FK |
| Quantidade | int, nullable | pra efeitos medidos em unidades (Dano/Alcance/Duração) |
| CustoPI | int | |

O mesmo par de tabelas (entrada + efeitos) se repete, como **cópia independente**, em `CharacterSpellAbilities`/`NpcSpellAbilities`/`CreatureSpellAbilities` (seção 6).

**RuneBankEntries** *(ver "[[Requisitos - Banco de Runas]]")* — biblioteca de Runas do GM; cada Runa criada numa ficha (Personagem ou NPC) tem uma cópia independente aqui.

| Coluna | Tipo | Nota |
|---|---|---|
| Id | PK | |
| GmId | FK → Users | |
| Nome | string | |
| Descricao | text | |
| Grau | int | |
| Tipo | enum TipoDeRuna (texto), nullable | `Arcana` ou `Negra`; NULL = sem tipo (Runas antigas ficam NULL). Só classificação exibida — ver R0009 do Banco de Runas |
| Disciplina | enum DisciplinaDeRuna (texto), nullable | `Adicao`/`Alteracao`/`Emissao`/`Manifestacao`; obrigatória ao criar/editar, NULL só em entradas antigas — ver R0010 do Banco de Runas |
| ImageId | FK → Images, nullable (`SetNull`) | imagem opcional da Runa |

  

# 5. Campanhas

*(ver "[[Requisitos - Campanha]]")*

**Campaigns**

| Coluna | Tipo |
|---|---|
| Id | PK |
| GmId | FK → Users |
| Nome | string |
| Descricao | text |
| ImageId | FK → Images, nullable (`SetNull`) — imagem da campanha |
| BonusDeCarga | decimal, padrão 0 (pode ser negativo) — somado ao Peso Máximo das fichas de Personagem da campanha (Campanha R0014) |

**CampaignMembers** — único por (`CampaignId`, `UserId`).

| Coluna | Tipo |
|---|---|
| Id | PK |
| CampaignId | FK → Campaigns |
| UserId | FK → Users |

**CampaignAttachments** — polimórfico via 6 FKs anuláveis (exatamente uma preenchida por linha); todas em cascade.

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

Cada tipo de ficha (Personagem, NPC, Criatura) é sua própria família de tabelas — a diferença real de campos entre elas (documentada como diffs) não cabe bem numa hierarquia TPH única. As três famílias replicam a mesma forma geral; as colunas abaixo cobrem a família **CharacterSheet** (Personagem) por extenso, e as duas seções seguintes só listam as **diferenças** de NPC e Criatura, no mesmo espírito dos próprios documentos de Requisitos. Toda tabela filha aponta para a ficha por `CharacterSheetId`/`NpcSheetId`/`CreatureSheetId` (cascade).

## 6.1 CharacterSheets (Personagem)

| Coluna | Tipo | Aba (ver Ficha de Personagem) |
|---|---|---|
| Id | PK | |
| CampaignId | FK → Campaigns | criada dentro de uma campanha (Campanha R0004) |
| OwnerId | FK → Users | |
| ImageId | FK → Images, nullable (`SetNull`) | 1.a |
| Nome | string, nullable | 1.a |
| Linhagem | enum, nullable | 1.a |
| Variante | enum, nullable | 1.a |
| Vocacao | enum, nullable | 1.a |
| SubVocacao | string, nullable | 1.a |
| Afinidade | enum AfinidadeElemental, nullable | 1.a |
| Propriedade | string, nullable | 1.a |
| Estrela | enum, nullable | Antecedentes (R0006) — uma das Estrelas de "[[As estrelas alkerianas]]" |
| HistoricoId | FK → Historicos, nullable (`SetNull`) | Antecedentes — referência ao vivo (ver legenda), nenhum campo é copiado para a ficha |
| Historia | text, nullable | Antecedentes — HTML do texto rico, sanitizado no servidor por uma allowlist antes de gravar; NULL = vazio. Uma imagem dentro do texto só é mantida quando o `src` é exatamente o endereço de uma `Image` do app (`/images/{Path}`) que quem salva pode usar ou que a História já continha; não há FK para essas imagens — o vínculo é o endereço no HTML. A galeria fica em `CharacterSheetHistoriaImages` (abaixo). |
| EquipmentKitId | FK → EquipmentKits, nullable (`SetNull`) | NULL até o jogador escolher um kit; depois disso, permanente (não pode ser trocado) |
| Nivel | int | 1.b |
| Circulo, Grau | int | 1.b — **vestigiais**: nada as grava (ficam 0); a Graduação exibida é computada em tempo de leitura a partir de `EAPAtual`/`Vocacao` |
| PossuiCoracaoDeMana | bool | 1.b |
| AfinidadeAdicional | int, default 0 | 2.c / R0007 — soma-se ao máximo de Vocação Arcana; o gasto e o máximo são computados em tempo de leitura |
| ExperienciaAtual | int | 1.b |
| EAPAtual | int | 1.b — **vestigial**: nem lida nem gravada; o VIS Atual exibido é computado em tempo de leitura (base do Nível na tabela de EAP + Âmbares Absorvidos por Rank) |
| NucleosRankF..NucleosRankS | int × 7 | 1.b (Âmbares Absorvidos) |
| PontosDeIgnicaoAtual | int | 1.b — saldo; a atualização geral da ficha só aplica sobre ele a diferença de Nível, a edição manual tem endpoint próprio |
| PontosDeIgnicaoTotal | int | 1.b — **vestigial**: nada a grava; o Total exibido é computado a partir do Nível e de `PontosDeIgnicaoBonusManual` |
| PontosDeIgnicaoBonusManual | int | 1.b — PI extra concedido pelo GM além do total do Nível |
| PontosDePericiaBonusCritico | int | 2.d — pontos de Perícia ganhos ao tirar o máximo no dado ("[[Ruína RPG - Sistema Básico]]" §2), descontados do Gasto Total do orçamento de Perícias |
| VitalidadeAtual, FocoAtual, AdrenalinaAtual, EstresseAtual | int | 1.c |
| SinaAtual | int | 1.c — Pontos de Sina, 0 a 3 (teto fixo) |
| Cobertura | enum Nenhuma \| Parcial \| Completa | 2.b |
| Ciclos | int | 5.a |
| ArcaRolada | int, nullable | 4.a — só relevante quando `Linhagem` = Humano; resolvida contra `ArcaEntries` (seção 10) |
| LastDismissedLevelUpLevel | int, nullable | R0002 — até qual Nível a caixa de aviso já foi fechada |

**CharacterSheetHistoriaImages** (aba Antecedentes — galeria de imagens da História; join como `DiaryEntryImages`, seção 7) — a linha some junto com a ficha ou com a imagem (cascade nas duas FKs).

| Coluna | Tipo | Observação |
|---|---|---|
| CharacterSheetId | FK → CharacterSheets | PK composta com `ImageId` — a mesma imagem entra uma única vez na galeria de uma ficha |
| ImageId | FK → Images | |
| Ordem | int | posição na galeria (0, 1, 2…), regravada a cada alteração |

**CharacterAttributes** — 1 linha por atributo (8 por ficha); único por (ficha, `Atributo`).

| Coluna | Tipo |
|---|---|
| Id | PK |
| CharacterSheetId | FK |
| Atributo | enum (2.a) |
| Gasto | int |
| Bonus | int |
| TemMaestria | bool |

**CharacterSkills** — linhas criadas sob demanda (2.d): só existe linha para a Perícia que já recebeu pontos ou um Atributo escolhido; a ausência de linha equivale a 0 pontos e ao Atributo sugerido pela Perícia. Único por (ficha, `PericiaId`).

| Coluna | Tipo |
|---|---|
| Id | PK |
| CharacterSheetId | FK |
| PericiaId | FK → Pericias (`Restrict`) |
| Gasto | int |
| AtributoEscolhido | enum Atributo, nullable — NULL = o `AtributoSugerido` da Perícia |

*(Modificador e Total não são colunas — são calculados.)*

**CharacterAffinities** — lista incremental (2.c). `SubElemento` é derivado pelo servidor a partir da Matriz Elemental (interseção de `Elemento` com `SegundaEssencia`) — ver Ficha de Personagem 2.c.

| Coluna | Tipo |
|---|---|
| Id | PK |
| CharacterSheetId | FK |
| Elemento | enum, nullable |
| ElementoValor | int, nullable |
| SubElemento | enum, nullable |
| SubElementoValor | int, nullable |
| SegundaEssencia | enum EssenciaBasica (Ar, Água, Fogo, Terra, Alma, Vida, Mundano), nullable |
| SegundaEssenciaValor | int, nullable |
| Experiencia | int, nullable |

**CharacterWeapons** — arsenal (3.a). Referência ao vivo ao Catálogo, exceto Durabilidade.

| Coluna | Tipo |
|---|---|
| Id | PK |
| CharacterSheetId | FK |
| ItemId | FK → Items (Tipo=Arma, `Restrict`) |
| IsEquipped | bool |
| DurabilidadeAtual | int — inicializada = Durabilidade Máxima do item (resolvida do Rank do item via `DurabilidadesPorRank`; 0 sem Rank ou se inquebrável) quando a linha é criada; editável depois, sempre limitada a [0, máximo atual] |

**CharacterArmorSlots** — 3 linhas fixas por ficha (3.b), único por (ficha, `Slot`). Referência ao vivo ao Catálogo, exceto Durabilidade.

| Coluna | Tipo |
|---|---|
| Id | PK |
| CharacterSheetId | FK |
| Slot | enum Capacete \| Superior \| Inferior |
| ItemId | FK → Items (Tipo=Armadura, `Restrict`), nullable |
| DurabilidadeAtual | int, nullable — nulo enquanto o slot está vazio; mesma inicialização de `CharacterWeapons` |

**CharacterShields** — arsenal (3.c). Referência ao vivo ao Catálogo, exceto Durabilidade.

| Coluna | Tipo |
|---|---|
| Id | PK |
| CharacterSheetId | FK |
| ItemId | FK → Items (Tipo=Escudo, `Restrict`) |
| IsEquipped | bool |
| DurabilidadeAtual | int — mesma inicialização de `CharacterWeapons` |

**CharacterSpellAbilities** — cópia independente (4.b).

| Coluna | Tipo |
|---|---|
| Id | PK |
| CharacterSheetId | FK |
| SourceBankEntryId | Guid, nullable, sem FK — a entrada do Banco escolhida (R0003 do Banco) ou a cópia criada do zero (R0001); usada para anexar a Magia/Habilidade à campanha na concessão (Campanha R0010). NULL em entradas antigas, criadas do zero antes desse vínculo. |
| Nome | string |
| Tipo | enum Magia \| Habilidade \| Passiva |
| Grau | int |
| GastoEmPI | int |
| Custo | int |
| Descricao | text |
| Categoria | enum Livre \| Vocacional \| DeClasse, nullable — só em Passiva (Tipo = Passiva) |
| Requisitos | jsonb, nullable — só em Passiva; ver "[[Requisitos - Banco de Magias e Habilidades]]" R0009 |
| CriadaNaFicha | bool, default false — verdadeiro quando a entrada foi montada do zero na ficha; só essas podem ser editadas. Entradas anteriores à 1.5.0 ficam falsas. |

**CharacterSpellAbilityEffects** — mesma forma de `SpellAbilityBankEffects`, com `CharacterSpellAbilityId` (FK → CharacterSpellAbilities, cascade) no lugar da FK do Banco.

**CharacterRunes** (4.d)

| Coluna | Tipo |
|---|---|
| Id | PK |
| CharacterSheetId | FK |
| Nome | string |
| Descricao | text |
| Grau | int |
| Tipo | enum TipoDeRuna (texto), nullable — `Arcana`/`Negra`; cópia do Tipo da entrada quando a Runa parte do banco. NULL = sem tipo |
| Disciplina | enum DisciplinaDeRuna (texto), nullable — cópia da Disciplina da entrada quando a Runa parte do banco; obrigatória ao montar do zero. NULL em Runas antigas |
| SourceBankEntryId | Guid, nullable, sem FK — a entrada do Banco de Runas escolhida (R0003) ou a cópia criada do zero (R0001). NULL em Runas antigas, criadas do zero antes desse vínculo. |
| ImageId | FK → Images, nullable (`SetNull`) — imagem opcional; cópia da imagem da entrada quando a Runa parte do banco |

**CharacterMasteries** (4.e)

| Coluna | Tipo |
|---|---|
| Id | PK |
| CharacterSheetId | FK |
| Nome | string |
| PericiaId | FK → Pericias (`Restrict`) |
| Atributo | enum |
| GastoMaestria | int |

**CharacterInventoryItems** (5.a) — referência ao vivo ao Catálogo.

| Coluna | Tipo |
|---|---|
| Id | PK |
| CharacterSheetId | FK |
| ItemId | FK → Items (Tipo=ItemGeral, `Restrict`) |
| Qtd | int |

**CharacterArtifacts** (5.b) — referência ao vivo ao Catálogo; limite de 3 por `TipoDeAlvo` validado na aplicação, não no schema.

| Coluna | Tipo |
|---|---|
| Id | PK |
| CharacterSheetId | FK |
| ArtifactItemId | FK → Items (Tipo=Artefato, `Restrict`) |

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
| TraitId | FK → Traits (`Restrict`) |
| Polaridade | enum Positiva \| Negativa |
| Especificacao | string, nullable — o complemento das Características com `RequerEspecificacao` |
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
| RequerEspecificacao | bool — a Característica pede um complemento (`CharacterTraits.Especificacao`) |
| IsCustomized | bool — true depois de criada/editada pelo Auditor de Regras; protege a linha de ser sobrescrita pelo re-seed a partir de Características.md |
| IsDeleted | bool — soft delete pelo Auditor de Regras; a linha continua existindo para o re-seed nunca recriá-la |
| UpdatedByUserId | Guid, nullable, sem FK |
| UpdatedAt | datetime, nullable |

  

**Historicos** — `Historico.md` convertido em tabela (dado estático, seedado a partir do documento).

| Coluna | Tipo |
|---|---|
| Id | PK |
| Nome | string |
| Descricao | text |
| PericiaMaisSeisId | FK → Pericias (`Restrict`) |
| PericiaMaisTresId | FK → Pericias (`Restrict`) |
| IsCustomized | bool — mesma lógica de `Traits.IsCustomized`, protegendo contra o re-seed a partir de Historico.md |
| IsDeleted | bool — soft delete, mesma lógica de `Traits.IsDeleted` |
| UpdatedByUserId | Guid, nullable, sem FK |
| UpdatedAt | datetime, nullable |

**EquipmentKits** — catálogo global (não por GM) de kits de equipamento inicial, cadastrado a partir de "[[Equipagem]]" e editável pelo Auditor de Regras (ver "[[Requisitos - Auditoria de Regras]]").

| Coluna | Tipo |
|---|---|
| Id | PK |
| Nome | string |
| Descricao | text — o parágrafo de sabor do kit |
| Ciclos | int — moeda concedida ao escolher o kit |
| IsDeleted | bool — soft delete |

**EquipmentKitFixedItems** — base global (não por GM) de itens fixos dos kits, mantida pelo Auditor de Regras (ver "[[Requisitos - Auditoria de Regras]]" R0014). Cada linha é um item completo, com os campos do formulário do catálogo de "[[Requisitos - Catálogo de Itens e Equipamentos]]" por Tipo, sem imagem. Único por (`Nome`, `Tipo`).

| Coluna | Tipo |
|---|---|
| Id | PK |
| Nome | string |
| Tipo | enum ItemTipo (ItemGeral, Arma, Armadura, Escudo ou Artefato) — não muda depois de criado |
| Dados | jsonb — os campos do item que variam por Tipo |
| Requisitos | jsonb, nullable — requisitos para equipar |
| PenalidadeDeRequisitos | jsonb, nullable — penalidade por não cumprir os requisitos |
| DetalhesIncompletos | bool — true nos itens que a conversão da versão 1.4.3 só conseguiu criar com o nome; deixa de valer quando o Auditor os edita |

**EquipmentKitItems** — linhas fixas de um `EquipmentKit`, referenciando um item da base `EquipmentKitFixedItems`. Ao aplicar o kit, o item é copiado para o catálogo do GM quando ele ainda não tem um de mesmo Nome e Tipo (ver "[[Requisitos - Catálogo de Itens e Equipamentos]]" R0012).

| Coluna | Tipo |
|---|---|
| Id | PK |
| KitId | FK → EquipmentKits (cascade) |
| FixedItemId | FK → EquipmentKitFixedItems (`Restrict`), nullable — um item usado por um kit não excluído não pode ser excluído; NULL nas linhas ainda não convertidas e nas de kits excluídos cujo item foi apagado |
| ArmorSlot | enum ArmorSlotType, nullable — obrigatório quando o item fixo é Armadura; vazio nos demais Tipos |
| Qtd | int |
| Nome (string, nullable), Tipo (enum ItemTipo), SubcategoriaHint (string, nullable) | **legados**, a serem removidos: eram a referência por Nome + Tipo anterior à base; a conversão da versão 1.4.3 os transformou em `FixedItemId` |

**EquipmentKitChoiceSlots** — linhas de escolha do jogador de um `EquipmentKit` (ex: "1 Arma Rank F de sua escolha"), resolvidas ao vivo contra o catálogo do GM no momento de aplicar o kit.

| Coluna | Tipo |
|---|---|
| Id | PK |
| KitId | FK → EquipmentKits (cascade) |
| Label | string — ex: "Arma", "Condutor" |
| Tipo | enum ItemTipo — Arma, Armadura, Escudo ou Artefato; nunca ItemGeral |
| ArmorSlot | enum ArmorSlotType, nullable — obrigatório quando `Tipo` = Armadura (em qual slot de armadura da ficha o item concedido é colocado); vazio nos outros Tipos |
| SubcategoriasCsv | string, nullable — valores aceitos separados por vírgula; cada um casa tanto com a Subcategoria legada em texto livre quanto com a Família parseada de um valor composto pelo construtor "Item Inicial" (ver "[[Requisitos - Catálogo de Itens e Equipamentos]]" R0013); NULL = qualquer uma |
| Rank | enum RankDeItem, nullable — NULL = qualquer Rank |
| Qtd | int |
| BonusSubcategoria | string, nullable — Subcategoria do item escolhido que ativa um bônus condicional |
| BonusFixedItemId | FK → EquipmentKitFixedItems (`Restrict`), nullable — item da base, do tipo Item Geral, concedido além da escolha só se `BonusSubcategoria` bater; NULL quando não há bônus, nas linhas ainda não convertidas e nas de kits excluídos cujo item foi apagado |
| BonusQtd | int, nullable |
| BonusNome | string, nullable — **legado**, a ser removido; substituído por `BonusFixedItemId` |

**SubcategoriaOptions** — catálogo global (não por GM) de valores de Categoria/Família usados pelo construtor de Subcategoria do cadastro de Item (ver "[[Requisitos - Catálogo de Itens e Equipamentos]]") e pelos slots de escolha da Equipagem; editável pelo Auditor de Regras na página de Auditoria de Equipagem.

| Coluna | Tipo |
|---|---|
| Id | PK |
| Tipo | enum ItemTipo — Arma, Armadura, Escudo ou Artefato; nunca ItemGeral |
| Facet | enum SubcategoriaFacet — Categoria ou Familia |
| Valor | string |
| IsDeleted | bool — soft delete |

## 6.2 NpcSheets — diferenças de CharacterSheets

*(ver "[[Requisitos - Ficha de NPCs]]")*

Mesma família completa de tabelas filhas (`NpcAttributes`, `NpcSkills`, `NpcAffinities`, `NpcWeapons`, `NpcArmorSlots`, `NpcShields`, `NpcSpellAbilities`/`NpcSpellAbilityEffects`, `NpcRunes`, `NpcMasteries`, `NpcInventoryItems`, `NpcArtifacts`, `NpcAffections`, `NpcTraits`, `NpcSheetHistoriaImages`) — um espelho 1:1 das tabelas de 6.1, prefixadas `Npc` em vez de `Character`, com a FK `NpcSheetId`. Diferenças na tabela raiz:

- `GmId`: FK → Users — o GM dono do bestiário.
- `OwnerId`: **nullable** (`SetNull`) — só setado se concedida a um jogador (Campanha R0010).
- `CampaignId`: **não existe** — o vínculo com campanha é via `CampaignAttachments` (seção 5); os toggles de Nome/Imagem públicos também vivem lá (podem diferir por campanha).
- Sem `PontosDeIgnicaoBonusManual` nem `PontosDePericiaBonusCritico`. `PontosDeIgnicaoTotal` é **vestigial**, como em CharacterSheets: desde a 1.5.0 o valor enviado na atualização é ignorado, e nada o grava além da cópia literal quando o GM concede a cópia de um NPC existente; a leitura devolve o valor guardado, sem uso. Diferente do Personagem, nenhum Total é computado a partir do Nível.
- `NpcSheetHistoriaImages`: ao conceder a cópia de um NPC existente (Campanha R0010), as linhas são copiadas para a ficha nova.
- Usa `ExperienciaAtual` para computar **Abate** e **Assistência**, com as mesmas fórmulas da Criatura (ver 6.3) — calculados em tempo de leitura, **não persistidos**, e entregues somente ao GM da ficha; o jogador dono de uma ficha concedida recebe `null`.

## 6.3 CreatureSheets — diferenças de CharacterSheets

*(ver "[[Requisitos - Ficha de Criaturas]]")*

Mesma lógica de 6.2: família de tabelas filhas espelhando 6.1 (prefixadas `Creature`, FK `CreatureSheetId`), exceto onde dito abaixo. `CreatureSpellAbilities`/`CreatureSpellAbilityEffects`, `CreatureMasteries`, `CreatureArtifacts`, `CreatureAffections`, `CreatureArmorSlots` e `CreatureShields` existem sem alteração de forma.

- `GmId`: FK → Users, como em NpcSheets.
- `OwnerId`: nullable (`SetNull`), mesma regra de NpcSheets.
- `CampaignId`: não existe, mesma regra de NpcSheets.
- Troca `Linhagem`/`Variante`/`Vocacao`/`SubVocacao` por: `Raca` (string, nullable), `Arquetipo` (enum Fisico \| Arcano, nullable), `SubArquetipo` (string, nullable).
- Troca `Circulo`/`Grau`/`EAPAtual`/`NucleosRank*` por: `Rank` (enum F..S, nullable).
- `PontosDeIgnicao`: **um único int**, não par atual/total; sem `PontosDeIgnicaoBonusManual` nem `PontosDePericiaBonusCritico`.
- A raiz guarda só `Id`, `GmId`, `OwnerId`, `ImageId`, `Nome`, `Raca`, `Arquetipo`, `SubArquetipo`, `Afinidade`, `Rank`, `Nivel`, `ExperienciaAtual`, `PontosDeIgnicao`, `VitalidadeAtual`, `FocoAtual`, `AdrenalinaAtual`, `Cobertura` e `LastDismissedLevelUpLevel`. Portanto **não tem** `Propriedade`, `Estrela`, `HistoricoId`, `Historia`, `EquipmentKitId`, `PossuiCoracaoDeMana`, `AfinidadeAdicional`, `EstresseAtual`, `SinaAtual`, `Ciclos` nem `ArcaRolada`.
- `CreatureAttributes.Atributo` usa um enum próprio de 6 valores (Força, Vigor, Agilidade, Destreza, Astúcia, **Ego**), não o de 8 valores do Personagem.
- `CreatureSkills.PericiaId` só permite as Perícias com `DisponivelParaCriaturas` (R0005 e Auditoria de Regras R0012) — restrição de aplicação, não de schema.
- Sem equivalente de `CharacterAffinities` (não existe aba de Afinidades pra Criatura), de `CharacterRunes` nem de `CharacterSheetHistoriaImages`.
- `CreatureWeapons`: `ItemId` **nullable** — quando nulo, usa `ManualNome` (string), `ManualTipoDeDano` (enum), `ManualDados` (string) e `ManualDano` (int), todas nullable (ataque natural, sem Alcance/Crítico/Rank/Durabilidade); `DurabilidadeAtual` também é nullable e fica nula nesse caso.
- `CreatureInventoryItems` → `CreatureSpoils` (Espólios): `ItemId` (FK → Items, `Restrict`), `Qtd` (int) e `DT` (int).
- `ExperienciaAtual` é usada para computar **Abate** = `piso(ExperienciaAtual × 0,15)` e **Assistência** = `piso(ExperienciaAtual × 0,12)` — calculados, não persistidos.
- `CreatureTraits` diverge de `CharacterTraits`/`NpcTraits`: `TraitId` **nullable**, ganha `CreatureExclusiveTraitId` (FK → CreatureExclusiveTraits, `Restrict`, nullable) — exatamente uma das duas é setada por linha, mesmo padrão de `EncounterParticipants.Source*SheetId` (seção 8) — e não tem `IsRacial`/`RacialVariante`.

**CreatureExclusiveTraits** — catálogo global e independente de `Traits` (6.1), com as mesmas colunas exceto `IsCustomized`: sem documento-fonte equivalente a `Características.md`, não há re-seed do qual proteger uma linha editada. Só `CreatureTraits` referencia esta tabela — Personagem, NPC e o Compêndio de Regras nunca a leem.

| Coluna | Tipo |
|---|---|
| Id | PK |
| Nome | string |
| Descricao | text |
| Custo | int |
| Polaridade | enum Positiva \| Negativa |
| RequerEspecificacao | bool |
| IsDeleted | bool — soft delete pelo Auditor de Regras |
| UpdatedByUserId | Guid, nullable, sem FK |
| UpdatedAt | datetime, nullable |


## 6.4 SheetLogEntries — log de alterações de ficha

*(ver "[[Requisitos - Campanha]]", R0016)*

Uma linha por alteração registrada numa ficha de Personagem, NPC ou Criatura. **Exatamente uma** das três FKs de ficha é preenchida por linha — mesmo padrão de `EncounterParticipants` (seção 8). Excluir a ficha apaga seu log em cascata. As linhas são escritas apenas pelo interceptor de salvamento (ver "[[Requisitos - Técnico]]" R0012), nunca por endpoint próprio.

| Coluna | Tipo |
|---|---|
| Id | PK |
| CharacterSheetId | FK → CharacterSheets, nullable, cascade |
| NpcSheetId | FK → NpcSheets, nullable, cascade |
| CreatureSheetId | FK → CreatureSheets, nullable, cascade |
| OccurredAt | datetime (UTC) |
| ActorUserId | Guid — usuário que fez a alteração (sem FK: o log sobrevive à conta) |
| ActorNickname | string — apelido do autor no momento da alteração |
| ActorIsGm | bool |
| Categoria | enum Geral \| RecursoVFA \| Estresse (texto) |
| Acao | enum Alterou \| Adicionou \| Removeu \| Criou \| Expirou (texto; Expirou só para Condições) |
| Secao | string — seção/aba da ficha afetada |
| Campo | string, nullable |
| ItemNome | string, nullable — nome do item adicionado/removido |
| ValorAntes | string, nullable |
| ValorDepois | string, nullable |
| Automatico | bool — escrita feita pelo servidor por conta própria dentro da ação do autor (corte do Atual por uma Condição, expiração); default false |

Índices: `(CharacterSheetId, OccurredAt DESC)`, `(NpcSheetId, OccurredAt DESC)` e `(CreatureSheetId, OccurredAt DESC)`.

  

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

**DiaryEntryImages** (join, PK composta `(DiaryEntryId, ImageId)` — imagens podem ser upload novo ou referência a uma `Image` existente; ambos os casos só criam uma linha aqui)

| Coluna | Tipo |
|---|---|
| DiaryEntryId | FK → DiaryEntries |
| ImageId | FK → Images |

**DiaryEntryRecipients** (join, PK composta `(DiaryEntryId, UserId)` — só populada quando `IsSecretNote = true`)

| Coluna | Tipo |
|---|---|
| DiaryEntryId | FK → DiaryEntries |
| UserId | FK → Users |
| ReadAt | datetime, nullable — nulo = não lida ([[Requisitos - Campanha]] R0015) |

  

# 8. Gerenciador de Encontros

*(ver "[[Requisitos - Gerenciador de Encontros]]")*

**Encounters**

| Coluna | Tipo |
|---|---|
| Id | PK |
| CampaignId | FK → Campaigns |
| Nome | string, nullable |
| Status | enum (texto) — `Preparacao`, `EmAndamento` ou `Finalizado`; padrão `Preparacao` |
| CurrentRound | int — turno atual; `0` enquanto o encontro está em `Preparacao` (nunca iniciou) |
| CurrentParticipantId | FK → EncounterParticipants, nullable (`SetNull`) — de quem é a vez |
| FinalizadoEm | datetime, nullable — preenchido quando `Status = Finalizado` |
| xmin | coluna de sistema do PostgreSQL (não criada pela migração), usada como token de concorrência otimista: de duas ações de vez ou de fase simultâneas sobre o mesmo encontro, só uma é aplicada, a outra responde 409 |

**EncounterLogEntries** — log do encontro, gravado só com o encontro em andamento.

| Coluna | Tipo | Nota |
|---|---|---|
| Id | PK | |
| EncounterId | FK → Encounters, cascade | índice `(EncounterId, Ordem)` |
| Sequencia | bigint, identity | desempate quando duas linhas têm a mesma `Ordem` |
| Ordem | bigint | ordem do log (valor estritamente crescente gerado na criação da linha) |
| OccurredAt | datetime | |
| Turno | int | turno em que a linha foi gravada |
| VezParticipantId | Guid, nullable | de quem era a vez; sem FK, o participante pode ser removido depois |
| VezNome | string, nullable | snapshot do nome |
| Tipo | enum (texto) | `Iniciado`, `Finalizado`, `Reaberto`, `NovoTurno`, `VezPassada`, `VezVoltou`, `VezEscolhida`, `ParticipanteAdicionado`, `ParticipanteRemovido`, `CampoAlterado`, `CondicaoAdicionada`, `CondicaoRemovida`, `RecursoDaFicha`, `CondicaoExpirada`, `AjusteDeCondicao` |
| ParticipanteNome | string, nullable | |
| Campo | string, nullable | |
| ValorAntes | string, nullable | |
| ValorDepois | string, nullable | |
| Texto | string, nullable | |

**EncounterParticipants**

| Coluna | Tipo | Nota |
|---|---|---|
| Id | PK | |
| EncounterId | FK → Encounters, cascade | |
| SourceCharacterSheetId | FK → CharacterSheets, nullable (`SetNull`) | no máximo uma das três colunas `Source*SheetId` é setada (R0002) |
| SourceNpcSheetId | FK → NpcSheets, nullable (`SetNull`) | idem |
| SourceCreatureSheetId | FK → CreatureSheets, nullable (`SetNull`) | idem |
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
| EncounterParticipantId | FK → EncounterParticipants, cascade |
| Texto | string |

Legado: só pode ser reduzida. Condições novas vêm do catálogo (`Condicoes`, seção 12) e ficam em `EncounterParticipantCondicoes`.

**EncounterParticipantCondicoes** — uma Condição do catálogo aplicada a um participante. **Cópia independente** (padrão 1 da legenda): editar ou apagar a entrada do catálogo não altera a que já foi aplicada.

| Coluna | Tipo | Nota |
|---|---|---|
| Id | PK | |
| EncounterParticipantId | FK → EncounterParticipants, cascade | índice |
| CondicaoId | Guid, nullable | só procedência (sem FK); serve para recusar a mesma Condição duas vezes no participante |
| Nome, Descricao, Icone | string | snapshot da entrada do catálogo |
| AplicadaNoTurno | int | `CurrentRound` ao aplicar; 0 se na preparação (a contagem começa no Turno 1) |
| DuracaoTurnos | int, nullable | a do catálogo, ou a escolhida ao aplicar; nulo = sem prazo |
| Modificadores | jsonb | lista de linhas `{LinhaId, Alvo, AlvoEspecifico, TipoDeValor, Valor, Momento, Maestrias}` com o valor como o GM ajustou; `LinhaId` (Guid) é estável; `Maestrias` (lista de ids de linhas de Maestria da ficha, ou nulo = todas) só numa linha de Maestria que o GM restringiu ao aplicar |
| Desabilitacoes | jsonb | por categoria (`Arma`, `Armadura`, `Escudo`, `Artefato`, `Magia`, `Habilidade`, `Runa`, `Passiva`): lista vazia = a categoria inteira; senão os ids das linhas da ficha |
| AjustesAplicados | jsonb | lista `{LinhaId, Turno}` dos ajustes de valor atual já feitos (Turno 0 = linha "ao aplicar"; uma linha "a cada vez" roda no máximo uma vez por Turno, ver [[Requisitos - Gerenciador de Encontros]] R0013); começa vazia |

  

# 9. Compêndio de Regras

*(ver "[[Requisitos - Compêndio de Regras]]")*

Sem tabelas próprias — o conteúdo vem de `Docs/Sistema RPG/` (com as sobrescritas do Auditor, seção 11). A indexação para busca (R0001–R0003 daquele documento) é construída em memória a partir dos arquivos-fonte, não persistida. Exceção: **Traits** (seção 6.1) já existe como tabela porque é referenciada por FK de `CharacterTraits` — o Compêndio busca nela em vez de duplicar.

  

# 10. Habilidades Raciais

*(ver "[[Requisitos - Habilidades Raciais]]")*

**RacialAbilityOverrides** — sobrescrita do GM pro Nome/Descrição padrão de uma Variante (R0001); o padrão vale quando a linha não existe. Única por (`GmId`, `Variante`).

| Coluna | Tipo |
|---|---|
| Id | PK |
| GmId | FK → Users |
| Variante | enum (mesmo enum de 6.1, 8 valores, incluindo a variante solar de Alóra) |
| Nome | string |
| Descricao | text |
| NomeDaVariante | string, nullable — só usado pela variante solar de Alóra: o nome dado pelo GM, que a libera nas fichas quando preenchido (R0005) |

**RacialTraitOverrides** — sobrescrita do GM pras opções de Característica Gratuita/Obrigatória de uma Variante (R0004); o padrão de "[[Ruína RPG - Sistema Básico]]" §7 vale quando a linha não existe. Única por (`GmId`, `Variante`); cada lista de opções é um JSON (array de {TraitNome, Especificacao}), não linhas filhas — tamanho pequeno e fixo, nunca consultado pelo conteúdo.

| Coluna | Tipo |
|---|---|
| Id | PK |
| GmId | FK → Users |
| Variante | enum (mesmo enum de 6.1, 8 valores) |
| GratuitaOptionsJson | text (JSON) |
| ObrigatoriaOptionsJson | text (JSON) |

**ArcaTabelas** — o dado escolhido pelo GM para a sua tabela de Arcas (Habilidades Raciais R0002). Ausência de linha = D20.

| Coluna | Tipo |
|---|---|
| GmId | PK, FK → Users (cascade) |
| Dado | int — uma das faces D6, D8, D10, D12, D20 ou D100 (6, 8, 10, 12, 20, 100) |

**ArcaEntries** — a "tabela de Arcas" (1d{dado}) referenciada pelo Racial de Sinir/Laonir, conteúdo livre do GM (R0002). Única por (`GmId`, `Roll`); ausência de linha para um Roll = "não cadastrada". Reduzir o dado só esconde as linhas com Roll acima dele: elas não são apagadas e voltam se o dado aumentar.

| Coluna | Tipo |
|---|---|
| Id | PK |
| GmId | FK → Users |
| Roll | int, 1 a 100 (o máximo depende do dado do GM em `ArcaTabelas`) |
| Nome | string |
| Descricao | text |

**ArcaEvolucoes** — evoluções de uma Arca liberadas por nível (Habilidades Raciais R0006). Apagar a Arca apaga suas evoluções.

| Coluna | Tipo |
|---|---|
| Id | PK |
| ArcaEntryId | FK → ArcaEntries (cascade) |
| Nivel | int, de 1 ao último nível de `NiveisProgressao` |
| Descricao | text |
| CriadaEm | datetime — desempate entre evoluções do mesmo nível |

  

# 11. Auditoria de Regras

*(ver "[[Requisitos - Auditoria de Regras]]")*

**RulebookDocumentOverrides** — sobrescrita do texto Markdown de um documento do Livro de Regras (R0002); o padrão embutido no build vale quando a linha não existe. Uma linha por Slug (não por GM — vale pro servidor inteiro).

| Coluna | Tipo |
|---|---|
| Id | PK |
| Slug | string, único — identifica o documento (ex.: "sistema-basico", "graus-e-circulos", "tabela-de-niveis") |
| MarkdownText | text |
| UpdatedByUserId | FK → Users (`Restrict`), nullable — NULL = escrita do sistema (ex.: a sincronização de Efeitos na inicialização do servidor — R0006) |
| UpdatedAt | datetime |

**Efeitos** — o catálogo de Efeitos de "[[GRAUS & CÍRCULOS]]" que uma Magia/Habilidade compra com PI (R0006/R0007). Global; semeado a partir de uma lista no código (não do Markdown), com a mesma convenção `IsCustomized`/`IsDeleted` de `Traits`. As linhas de efeito das Magias/Habilidades (`*SpellAbilityEffects`, `SpellAbilityBankEffects`) apontam pra cá pelo `Nome`, sem FK.

| Coluna | Tipo |
|---|---|
| Id | PK |
| Nome | string |
| Grau | int, 1 a 9 — o Grau/Círculo a partir do qual o Efeito fica disponível |
| Descricao | text |
| TipoDeCusto | enum Fixo \| PorUnidade \| Manual \| ManualPorUnidade \| DerivadoDeOutroEfeito |
| CustoFixo | int, nullable — custo do tipo Fixo |
| CustoPorUnidade | int, nullable — custo por unidade (PorUnidade e DerivadoDeOutroEfeito) |
| UnidadeLabel | string, nullable — rótulo da unidade quando há Quantidade |
| QuantidadeDerivadaDeEfeito | string, nullable — no tipo DerivadoDeOutroEfeito, o Nome do Efeito da mesma Magia/Habilidade cuja Quantidade é usada |
| MaxUnidades | int, nullable — teto de Quantidade; NULL = sem teto |
| MaxEscalaPorGrau | bool — o teto é multiplicado pelo Grau da Magia/Habilidade |
| MaxContandoAPartirDoGrau | int, nullable — com `MaxEscalaPorGrau`, o Grau a partir do qual a multiplicação conta |
| CustoAlternativo | int, nullable — custo que substitui o normal a partir de `CustoAlternativoAPartirDoGrau` |
| CustoAlternativoAPartirDoGrau | int, nullable |
| PreRequisitosJson | text (JSON), nullable — grupos de pré-requisito: lista de grupos "ou", todos exigidos ("e") |
| IsCustomized | bool |
| IsDeleted | bool — soft delete (uma Magia/Habilidade pode já usar o Nome) |
| UpdatedByUserId | Guid, nullable, sem FK |
| UpdatedAt | datetime, nullable |

**Pericias** — perícias do sistema (R0012). Nunca apagadas de verdade.

| Coluna | Tipo |
|---|---|
| Id | int, PK (0–38 = perícias iniciais; novas = maior Id + 1) |
| Chave | string, único, imutável — identificador usado pela API e pelo Alvo de Artefatos |
| Nome | string, único entre as não removidas |
| Descricao | text, nullable |
| AtributoSugerido | enum Atributo, nullable |
| DisponivelParaCriaturas | bool |
| IsDeleted | bool |

**NiveisProgressao** — uma linha por nível da Tabela de Níveis (R0013). Preenchida uma vez, a partir de "[[Tabela de Níveis]]" e das tabelas de XP/EAP, quando vazia.

| Coluna | Tipo |
|---|---|
| Nivel | int, PK |
| OutrosBonus | text, nullable — linhas livres de bônus do nível |

**ColunasDeNivel** — colunas da tabela (as do sistema e as criadas pelo Auditor).

| Coluna | Tipo |
|---|---|
| Id | PK |
| Nome | string |
| Tipo | enum Acumulativa \| PorNivel (imutável) |
| ChaveDeSistema | string, nullable, único — preenchida nas colunas do sistema (não removíveis) |
| Ordem | int |
| IsDeleted | bool — reservado, não usado: remover uma coluna é exclusão física (apaga também os valores) |

**TabelaDeNiveisConfig** — configuração da Tabela de Níveis, linha única (R0013). Sem linha = padrão (tudo falso).

| Coluna | Tipo |
|---|---|
| Id | int, PK — sempre 1 |
| MostrarLimitesNoLivro | bool — se o Livro de Regras mostra a tabela "Limites e progressão" (padrão false) |

**ValoresDeNivel** — valor de uma célula; PK composta `(Nivel, ColunaId)`.

| Coluna | Tipo |
|---|---|
| Nivel | FK → NiveisProgressao (cascade) |
| ColunaId | FK → ColunasDeNivel (cascade) |
| Valor | int, nullable — vazio = 0 (Acumulativa) ou herda o nível anterior (Por nível, exceto XP e EAP) |


# 12. Condições

Catálogo global mantido pelo Auditor de Regras (Auditoria → Condições): uma Condição é um nome com uma lista de modificadores e uma lista de categorias desabilitadas, que o Gerenciador de Encontros aplica a participantes (cópia independente, padrão 1 da legenda). Começa vazio.

**Condicoes**

| Coluna | Tipo |
|---|---|
| Id | PK |
| Nome | string — único entre as linhas não apagadas (índice filtrado por `IsDeleted = false`) |
| Descricao | text |
| Icone | string — uma chave da lista fixa de ícones |
| DuracaoPadraoTurnos | int, nullable — vazio = até ser removida; quando informado, ≥ 1 |
| IsDeleted | bool — exclusão lógica, sempre permitida (as aplicadas são cópias independentes) |
| UpdatedByUserId | Guid, nullable, sem FK |
| UpdatedAt | datetime, nullable |

**CondicaoModificadores**

| Coluna | Tipo |
|---|---|
| Id | PK |
| CondicaoId | FK → Condicoes (cascade) |
| Ordem | int |
| Alvo | enum `AlvoDeCondicao` (texto): Atributo \| Pericia \| SubAtributo \| RecursoTotal \| RecursoAtual \| AfinidadeElemental \| DanoElemental \| Dano \| Maestria |
| AlvoEspecifico | string, nullable — nome do `Atributo`; chave da Perícia (vazio = todas); constante de `SubAtributoAlvo`; `Vitalidade` \| `Foco` \| `Adrenalina` \| `Estresse`; nome do `Elemento` (vazio = geral); nome do `TipoDeDano` (vazio = todos); chave da Perícia da Maestria (vazio = todas). Obrigatório para Atributo, SubAtributo, RecursoTotal e RecursoAtual |
| TipoDeValor | enum `TipoDeValorDeCondicao` (texto): Fixo \| Percentual; percentual entre −100 e 1000 |
| Valor | int, com sinal, diferente de zero |
| Momento | enum `MomentoDeCondicao` (texto): AoAplicar \| ACadaVez, nullable — preenchido se e somente se `Alvo = RecursoAtual` |

**CondicaoDesabilitacoes**

| Coluna | Tipo |
|---|---|
| Id | PK |
| CondicaoId | FK → Condicoes (cascade) |
| Categoria | enum `CategoriaDesabilitavel` (texto): Arma \| Armadura \| Escudo \| Artefato \| Magia \| Habilidade \| Runa \| Passiva; sem repetir na mesma Condição |

Uma Condição precisa de ao menos um modificador ou uma desabilitação.
