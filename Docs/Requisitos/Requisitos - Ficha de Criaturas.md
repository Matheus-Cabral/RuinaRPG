> Esta página se baseia no bloco **PAINEL DO *GM*** (ferramenta 2, "Bestiário do GM") do "[[01 - Visão Geral.canvas]]" e na "[[Ruína RPG - Ficha de Criaturas.pdf]]".

  

> **Documento relacionado**: uma Ficha de Criatura usa a **mesma estrutura geral** de "[[Requisitos - Ficha de Personagem]]" — mesmas 5 abas, mesma convenção de campos —, mas é uma versão **simplificada** com vários detalhes diferentes. Este documento assume a estrutura de lá como base e só registra as **diferenças**; qualquer campo não mencionado aqui segue exatamente igual ao de "[[Requisitos - Ficha de Personagem]]".

  

# **R0001** - Fichas de Criatura são de acesso exclusivo do GM.

**Descrição**: Igual a "[[Requisitos - Ficha de NPCs]]" R0001 — não há "jogador dono". O GM tem acesso total: visualização, criação, edição e exclusão de qualquer campo, a qualquer momento.

**Exceção**: mesma exceção de "[[Requisitos - Ficha de NPCs]]" R0001 — quando concedida a um jogador (ver "[[Requisitos - Campanha]]" R0010, útil para pets e invocações), passa a ter um jogador dono e a seguir o modelo de edição da Ficha de Personagem.

  

# **R0002** - As fichas de Criatura do GM devem ser listadas com filtros avançados.

**Descrição**: A página "Bestiário do GM" exibe uma lista de todas as fichas de Criatura criadas pelo GM, com filtros combináveis por **Nome**, **Raça**, **Arquétipo** e **Rank** (ver 1.a abaixo), além de **Campanha vinculada** (ver "[[Requisitos - Campanha]]", R0006).

  

# **R0003** - Um jogador vê, no máximo, o Nome e a Imagem da Criatura anexada a uma campanha, cada um liberado individualmente pelo GM.

**Descrição**: Mesmo comportamento de "[[Requisitos - Ficha de NPCs]]" R0004 — o jogador nunca vê a ficha completa; no máximo, Nome e Imagem, cada um ligado/desligado independentemente pelo GM, substituindo o toggle único de R0008 da "[[Requisitos - Campanha]]" para anexos do tipo Ficha de Criatura. Quando a Imagem está liberada, também pode ser referenciada por um jogador em outros campos de imagem (ver "[[Requisitos - Catálogo de Itens e Equipamentos]]" R0010).

  

# **R0004** - Diferenças da aba Informações Básicas.

**Descrição**:

**1.a Identidade** — diferenças de "[[Requisitos - Ficha de Personagem]]":

- *Linhagem*, *Variante*, *Vocação*, *Classe*, *Trabalho* e *Propriedade* não existem na Ficha de Criatura. Em vez delas:
  - *Raça*: text input livre (não há uma tabela fixa de raças de criatura).
  - *Arquétipo*: dropdown com os 2 valores da "[[Tabela de Arquetipos]]": **Físico** ou **Arcano**. Substitui o papel da Vocação — determina as colunas de Vida e Arcana usadas na progressão (ver Recursos abaixo).
  - *Sub Arquétipo*: text input livre (não há uma tabela equivalente à Tabela de Classes para criaturas).
- *Afinidade*: mesmo campo do Personagem (dropdown de Elementos/Sub-Elementos).
- *Rank*: dropdown com os valores **F**, **E**, **D**, **C**, **B**, **A**, **S**. Substitui inteiramente o sistema de Círculo/Grau/VIS/Âmbares Absorvidos do Personagem (ver 1.b abaixo) — a Criatura tem uma classificação de poder fixa, não uma progressão calculada. É o mesmo Rank referenciado em "Âmbares Absorvidos" (Personagem, 1.b) e em "Contrato Mágico" ("[[GRAUS & CÍRCULOS]]" §1).
- *Efeito de Batalha*: mesmo campo pendente do Personagem (3.d).

**1.b Nível e Progressão** — diferenças:

- *Nível* e *Experiência atual*: mesmos campos do Personagem.
- *Experiência dada*: dois campos calculados, não editáveis, nesta ordem — **Kill** e **Assistência** — quanto de XP a Criatura concede aos jogadores ao ser derrotada, conforme o papel de cada jogador na derrota. `Kill = piso(Experiência atual × 0,15)`; `Assistência = piso(Experiência atual × 0,12)` (arredondados para baixo; *Experiência atual* é o campo da própria Criatura, acima). Campo novo, sem equivalente no Personagem.
- *Graduação* (Círculo/Grau), *VIS atual*, *Para o próximo* e *Âmbares Absorvidos* não existem na Ficha de Criatura — substituídos pelo campo único *Rank* (1.a).
- *Pontos de Ignição*: campo numérico inteiro simples (não é um par atual/total como no Personagem) — o total de PI disponível para montar as Magias/Habilidades da Criatura (ver R0007).

**1.c Recursos** — diferenças:

- *Foco* é rotulado **Arcana** na Ficha de Criatura (mesmo campo, mesma fórmula: `(Astúcia × 2) + Status de classe Arcana`).
- *Vitalidade*: mesma fórmula do Personagem, `(Vigor × 2) + Status de classe Vida`.
- Em ambos os casos, "Status de classe" vem da "[[Tabela de Arquetipos]]" (cruzando Arquétipo × Nível), não da Tabela de Vocação/Classes.
- *Adrenalina*: mesmo campo e fórmula do Personagem.
- *Estresse* não existe na Ficha de Criatura.

  

# **R0005** - Diferenças da aba Atributos & Perícias.

**Descrição**:

**2.a Atributos** — a Criatura tem **6 atributos**, não 8: **Força**, **Vigor**, **Agilidade**, **Destreza**, **Astúcia** e **Ego**. *Instinto*, *Influência* e *Vontade* não existem — *Ego* é a fusão dos três num único atributo. Por ora, nenhuma fórmula do sistema depende de Ego especificamente (nem dos três que ele substitui, no contexto de Criatura); campo tratado como os demais atributos (mesma estrutura Gasto/Bônus/Maestria/Total e mesmas fórmulas de Total do Personagem), mas sem uso definido nas fórmulas ainda.

**2.b Sub-Atributos** — mesmos campos e fórmulas do Personagem (Iniciativa, Movimentação, Esquiva Natural, Defesa Natural, Cobertura, Redução Física, Redução Mágica), com as seguintes diferenças:

- *Resistência Física* e *Resistência Arcana* existem na Ficha de Criatura (diferente do Personagem, que não os tem). Por ora, nenhuma fórmula do sistema depende deles — campos previstos porém não implementados até existir regra.
- *Dano Cortante* aparece na ficha real como um sub-atributo próprio da Criatura. Mesmo tratamento de *Dano de Briga* no Personagem: campo previsto porém sem fórmula definida ainda.
- *Eficiência Elemental* e *Dano Elemental* não aparecem na Ficha de Criatura (já eram pendentes no Personagem).

**2.c Afinidades** não existe na Ficha de Criatura — a lista incremental de Elemento/Sub-Elemento/Caminho e Experiência do Personagem não tem equivalente aqui; a Criatura tem apenas o campo único *Afinidade* em 1.a.

**2.d Perícias** — lista fixa mais curta que a do Personagem, com apenas: Acrobacia, Artefatos Mágicos, Atletismo, Brigar, Empatia, Enganação, Força de Vontade, Fortitude, Furtividade, Intimidação, Intuição, Investigação, Navegação, Ocultismo, Percepção, Pontaria, Prontidão, Reflexos, Sedução e Sobrevivência. Mesma estrutura Gasto/Modificador/Atributo/Total do Personagem; o dropdown de Atributo oferece apenas os 6 atributos de Criatura (2.a).

  

# **R0006** - Diferenças da aba Combate.

**Descrição**:

**3.a Armas e Condutores** — modelo híbrido: ao adicionar uma linha, o GM escolhe entre:

- **Vincular ao Catálogo**: mesmo comportamento do Personagem (3.a) — escolhe uma Arma do "[[Requisitos - Catálogo de Itens e Equipamentos]]", todos os campos (Nome, Tipo de Dano, Alcance, Dados, Dano, Crítico, Tier, Durabilidade Atual/Máximo) vêm preenchidos automaticamente, com o mesmo comportamento de Durabilidade Atual editável e independente por linha.
- **Preencher manualmente**: para ataques naturais (ex: garra, mordida) sem vínculo com o Catálogo. Campos digitados diretamente: *Nome*, *Tipo de Dano* (dropdown, mesmos valores do Catálogo), *Dados* e *Dano*. Sem Alcance, Crítico, Tier ou Durabilidade — ataque natural não tem durabilidade.

O restante (Peso quando vinculado ao Catálogo, seleção de Equipada) segue igual ao Personagem.

**3.b Armaduras** e **3.c Escudos**: sem alteração em relação ao Personagem.

**3.d Efeito de Batalha** e **3.e Iniciativa**: sem alteração.

  

# **R0007** - Diferenças da aba Magias & Habilidades.

**Descrição**:

- **4.a Habilidade Racial** não existe na Ficha de Criatura.
- **4.b Magias & Habilidades**: mesma estrutura do Personagem (Nome, Tipo, Grau, Efeitos, Gasto em PI, Custo em Arcana, Descrição — ver "[[Requisitos - Ficha de Personagem]]" 4.b), com uma diferença: o Grau de cada entrada é limitado pelo *Rank* da Criatura (1.a) em vez do Círculo/Grau do Personagem. A equivalência exata entre Rank e Grau máximo ainda não está definida nas regras — campo de validação previsto porém pendente até essa equivalência existir.
- **4.c Contratos** e **4.d Runas** não existem na Ficha de Criatura.
- **4.e Maestrias**: mesma estrutura do Personagem; o dropdown de Atributo oferece apenas os 6 atributos de Criatura (2.a).

  

# **R0008** - Diferenças da aba Posses.

**Descrição**:

**5.a Inventário é substituído por Espólios** — o que a Criatura larga ao ser derrotada e saqueada, não um inventário que ela carrega e usa. Não há campo de Ciclos aqui (Criaturas não mantêm um saldo de moeda). Lista incremental, cada linha:

- *Item*: dropdown/busca vinculado a um item do "[[Requisitos - Catálogo de Itens e Equipamentos]]" (qualquer Tipo).
- *Qtd*: campo numérico inteiro ≥ 1.
- *Custo*: herdado do Preço do item (somente leitura, em Ciclos, ver "[[Requisitos - Catálogo de Itens e Equipamentos]]" R0008).
- *Custo Total*: campo calculado, não editável. `Custo Total = Custo × Qtd`.
- *DT*: campo numérico — a Dificuldade de Teste que o jogador precisa superar num teste de Saquear para obter aquele item do espólio.

**5.b Artefatos**: mesma estrutura do Personagem; quando o Tipo é Atributo, o Alvo oferece apenas os 6 atributos de Criatura (2.a).

**5.c Afeições** e **5.d Características**: sem alteração em relação ao Personagem.
