> Esta página se baseia no bloco **PAINEL DO *GM*** (ferramenta 3, "NPCs do GM") do "[[01 - Visão Geral.canvas]]".

  

> **Documento relacionado**: uma Ficha de NPC usa a **mesma estrutura completa** de "[[Requisitos - Ficha de Personagem]]" — todas as 6 abas (Informações Básicas, Atributos & Perícias, Combate, Magias & Habilidades, Posses, História), todos os campos, fórmulas e convenções de lá se aplicam aqui integralmente. Este documento só registra o que é **diferente**.

  

# **R0001** - Fichas de NPC são de acesso exclusivo do GM.

**Descrição**: Ao contrário da Ficha de Personagem, uma Ficha de NPC não tem "jogador dono". O GM que a criou tem acesso total: visualização, criação, edição e exclusão de qualquer campo, a qualquer momento — não há a distinção de "campos editáveis pelo jogador" vs. "criação/exclusão exclusiva do GM" que existe em "[[Requisitos - Ficha de Personagem]]".

**Exceção**: quando o GM concede a ficha a um jogador (ver "[[Requisitos - Campanha]]" R0010 — útil para pets e invocações), ela passa a ter um jogador dono e a seguir o modelo de edição da Ficha de Personagem.

  

# **R0002** - A estrutura da ficha é idêntica à Ficha de Personagem.

**Descrição**: Todas as abas, subgrupos, campos, fórmulas e convenções de campo (valor padrão do banco, placeholder em NULL, travessão para N/A) definidos em "[[Requisitos - Ficha de Personagem]]" se aplicam a uma Ficha de NPC sem alteração.

  

# **R0003** - As fichas de NPC do GM devem ser listadas com filtros avançados.

**Descrição**: A página "NPCs do GM" exibe uma lista de todas as fichas de NPC criadas pelo GM, com filtros combináveis por:

- **Nome**.
- **Linhagem / Vocação**: Linhagem, Variante, Vocação ou Classe (ver 1.a de "[[Requisitos - Ficha de Personagem]]").
- **Nível** (ver 1.b de "[[Requisitos - Ficha de Personagem]]").
- **Campanha vinculada**: em quais campanhas esse NPC foi anexado (ver "[[Requisitos - Campanha]]", R0006).

  

# **R0004** - Um jogador vê, no máximo, o Nome e a Imagem do NPC anexado a uma campanha, cada um liberado individualmente pelo GM.

**Descrição**: Quando um GM anexa uma Ficha de NPC a uma campanha (ver "[[Requisitos - Campanha]]", R0006 e R0008), um jogador membro dessa campanha nunca vê a ficha completa — o restante dela (atributos, perícias, combate, magias, posses) é sempre visível somente ao GM, e cabe aos jogadores catalogarem o que descobrem sobre o NPC em seus próprios diários (ver "[[Requisitos - Ficha de Personagem]]" 6).

No máximo, dois campos podem ser liberados: **Nome** e **Imagem do Personagem** (ver 1.a de "[[Requisitos - Ficha de Personagem]]"). Ao anexar (ou depois, a qualquer momento), o GM liga ou desliga cada um dos dois independentemente — pode liberar só o Nome, só a Imagem, os dois, ou nenhum (equivalente a totalmente privado). Isso substitui, para anexos do tipo Ficha de NPC, o toggle único público/privado de R0008 da "[[Requisitos - Campanha]]".

Quando a Imagem está liberada, ela também pode ser **referenciada** por um jogador em outros campos de imagem do sistema, em vez de reenviada (ver "[[Requisitos - Catálogo de Itens e Equipamentos]]" R0010).

  

# **R0005** - Editar Nível ou Experiência atual recalcula automaticamente o outro campo.

**Descrição**: Ao contrário do Personagem (onde só a Experiência atual é editável e o Nível é sempre calculado a partir dela — ver 1.b de "[[Requisitos - Ficha de Personagem]]"), na Ficha de NPC o Nível continua editável diretamente pelo GM. Editar um dos dois campos recalcula automaticamente o outro pelos mesmos limiares de XP em "[[Tabelas de XP, Atributos, Características e EAP]]": mudar o Nível ajusta a Experiência atual para o mínimo daquele nível; mudar a Experiência atual recalcula o Nível pela mesma regra de limiares do Personagem. Os dois campos nunca ficam inconsistentes entre si.

# **R0006** - O popup de Característica Racial (5.d) é resolvido pelo GM, não pelo NPC.

**Descrição**: O mecanismo é idêntico ao da Ficha de Personagem (ver "[[Requisitos - Ficha de Personagem]]" 5.d) — a Variante do NPC concede uma Característica Gratuita (e, para algumas Variantes, também uma Obrigatória) a custo 0, fora do orçamento de pontos. A única diferença é quem resolve a escolha: como a Ficha de NPC não tem jogador dono (R0001), é o **GM** quem vê e resolve o popup, não um jogador — mesma exceção de R0001 se aplica quando a ficha foi concedida a um jogador (aí é ele quem resolve, como na própria Ficha de Personagem).

# **R0007** - O total de Pontos de Atributo por Nível é exibido, mas não bloqueia o GM.

**Descrição**: Como no Personagem (ver 2.a de "[[Requisitos - Ficha de Personagem]]"), a seção *Atributos* exibe **Pontos de Atributo: gasto / disponíveis** — a soma do *Gasto* dos 8 atributos contra os "Pontos de Atributo" que o *Nível* da ficha já concedeu na "[[Tabela de Níveis]]" (criação + níveis). Diferente do Personagem, o valor **não é um limite**: o GM pode salvar um *Gasto* acima do total, e o contador apenas fica destacado em vermelho como aviso. O contador é recalculado ao salvar um atributo e ao mudar o *Nível* ou a *Experiência atual* (R0005).

# **R0008** - As Runas do NPC usam o Banco de Runas.

**Descrição**: A aba 4.d do NPC segue "[[Requisitos - Ficha de Personagem]]" 4.d sem alteração, inclusive a origem da Runa (do zero ou do "[[Requisitos - Banco de Runas]]") e a cópia automática para o banco do GM (R0001 do banco). O GM que gerencia o NPC escolhe qualquer entrada do próprio banco; quando o NPC foi concedido a um jogador (R0001, exceção), o jogador só escolhe entre as entradas públicas da campanha da concessão, e a Runa que ele cria também vira anexo público dessa campanha (R0007 do banco).

  

# **R0009** - A aba História do NPC é a última aba.

**Descrição**: A aba História segue "[[Requisitos - Ficha de Personagem]]" R0006 sem alteração (Estrela, Histórico e a história em texto formatado, com o mesmo salvamento automático e a mesma limpeza de HTML). Como a Ficha de NPC não tem Diário, a História é a última aba. Quando o GM concede a um jogador a cópia de um NPC existente ("[[Requisitos - Campanha]]" R0010), a História é copiada junto.
