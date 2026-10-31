> Esta página se baseia no bloco **PAINEL DO *GM*** (ferramenta 3, "NPCs do GM") do "[[01 - Visão Geral]]".

  

> **Documento relacionado**: uma Ficha de NPC usa a **mesma estrutura completa** de "[[Requisitos - Ficha de Personagem]]" — todas as 6 abas (Informações Básicas, Atributos & Perícias, Combate, Magias & Habilidades, Posses, Antecedentes), todos os campos, fórmulas e convenções de lá se aplicam aqui integralmente. Este documento só registra o que é **diferente**.

  

# **R0001** - Fichas de NPC são de acesso exclusivo do GM.

**Descrição**: Ao contrário da Ficha de Personagem, uma Ficha de NPC não tem "jogador dono". O GM que a criou tem acesso total: visualização, criação, edição e exclusão de qualquer campo, a qualquer momento.

**Exceção**: quando o GM concede a ficha a um jogador (ver "[[Requisitos - Campanha]]" R0010 — útil para pets e invocações), ela passa a ter um jogador dono e a seguir o modelo de edição da Ficha de Personagem.

  

# **R0002** — removido na 2.0.0

Coberto pelo preâmbulo deste documento: a estrutura da ficha é a de "[[Requisitos - Ficha de Personagem]]", sem alteração.

  

# **R0003** - As fichas de NPC do GM devem ser listadas com filtros avançados.

**Descrição**: A página "NPCs do GM" exibe uma lista de todas as fichas de NPC criadas pelo GM, com filtros combináveis por:

- **Nome**.
- **Linhagem / Vocação**: Linhagem, Variante, Vocação ou Classe (ver 1.a de "[[Requisitos - Ficha de Personagem]]").
- **Nível** (ver 1.b de "[[Requisitos - Ficha de Personagem]]").
- **Campanha vinculada**: em quais campanhas esse NPC foi anexado (ver "[[Requisitos - Campanha]]", R0006).

**Experiência dada** *(só GM)*: a ficha de NPC exibe dois campos calculados, não editáveis — **Abate** e **Assistência** — com as mesmas fórmulas da Criatura (ver "Experiência dada" em "[[Requisitos - Ficha de Criaturas]]"), aplicadas à *Experiência atual* do NPC. Ficam ao lado de Nível/Experiência atual e acompanham as mudanças dos dois; calculados ao vivo, nunca persistidos. O jogador que recebeu o NPC por concessão não os vê.

  

# **R0004** - Um jogador vê, no máximo, o Nome e a Imagem do NPC anexado a uma campanha, cada um liberado individualmente pelo GM.

**Descrição**: Quando um GM anexa uma Ficha de NPC a uma campanha (ver "[[Requisitos - Campanha]]", R0006 e R0008), um jogador membro dessa campanha nunca vê a ficha completa — o restante dela (atributos, perícias, combate, magias, posses) é sempre visível somente ao GM.

No máximo, dois campos podem ser liberados: **Nome** e **Imagem do Personagem** (ver 1.a de "[[Requisitos - Ficha de Personagem]]"). Ao anexar (ou depois, a qualquer momento), o GM liga ou desliga cada um dos dois independentemente — pode liberar só o Nome, só a Imagem, os dois, ou nenhum (equivalente a totalmente privado). Isso substitui, para anexos do tipo Ficha de NPC, o toggle único público/privado de R0008 da "[[Requisitos - Campanha]]".

Quando a Imagem está liberada, ela também pode ser **referenciada** por um jogador em outros campos de imagem do sistema, em vez de reenviada (ver "[[Requisitos - Catálogo de Itens e Equipamentos]]" R0010).

  

# **R0005** - Editar Nível ou Experiência atual recalcula automaticamente o outro campo.

**Descrição**: Diferente do Personagem (1.b de "[[Requisitos - Ficha de Personagem]]"), na Ficha de NPC o Nível é editável diretamente pelo GM. Editar um dos dois campos recalcula automaticamente o outro pelos mesmos limiares de XP em "[[Tabelas de XP, Atributos, Características e EAP]]": mudar o Nível ajusta a Experiência atual para o mínimo daquele nível; mudar a Experiência atual recalcula o Nível pela mesma regra de limiares do Personagem. Os dois campos nunca ficam inconsistentes entre si. Pontos de Ignição seguem 1.b do Personagem, também quando o GM edita o Nível diretamente; o NPC não tem bônus manual.

# **R0006** - O popup de Característica Racial (5.d) é resolvido pelo GM, não pelo NPC.

**Descrição**: Mesmo mecanismo de "[[Requisitos - Ficha de Personagem]]" 5.d. A única diferença é quem resolve a escolha: como a Ficha de NPC não tem jogador dono (R0001), é o **GM** quem vê e resolve o popup, não um jogador — mesma exceção de R0001 se aplica quando a ficha foi concedida a um jogador (aí é ele quem resolve, como na própria Ficha de Personagem).

# **R0007** - O total de Pontos de Atributo por Nível é exibido, mas não bloqueia o GM.

**Descrição**: Como no Personagem (ver 2.a de "[[Requisitos - Ficha de Personagem]]"), a seção *Atributos* exibe **Pontos de Atributo: gasto / disponíveis**. Diferente do Personagem, o valor **não é um limite**: o GM pode salvar um *Gasto* acima do total, e o contador apenas fica destacado em vermelho como aviso. O contador é recalculado ao salvar um atributo e ao mudar o *Nível* ou a *Experiência atual* (R0005).

# **R0008** - As Runas do NPC usam o Banco de Runas.

**Descrição**: A aba 4.d do NPC segue "[[Requisitos - Ficha de Personagem]]" 4.d sem alteração; num NPC concedido a um jogador (R0001, exceção), a campanha é a da concessão ("[[Requisitos - Banco de Runas]]" R0007).

  

# **R0009** - A aba Antecedentes do NPC é a última aba.

**Descrição**: A aba Antecedentes segue "[[Requisitos - Ficha de Personagem]]" R0006 sem alteração. Como a Ficha de NPC não tem Diário, a aba Antecedentes é a última. Quando o GM concede a um jogador a cópia de um NPC existente ("[[Requisitos - Campanha]]" R0010), a História é copiada junto, com as imagens do texto e a galeria.

  

# **R0010** - O limite e as regras das Afinidades são exibidos, mas não bloqueiam o GM.

**Descrição**: O contador **Distribuídos {gasto}/{máximo}** e o campo *Afinidade Adicional* seguem "[[Requisitos - Ficha de Personagem]]" R0007 sem alteração no cálculo. Diferente do Personagem, o máximo **não é um limite**: o GM pode salvar Afinidades acima dele, e o contador apenas fica destacado em vermelho como aviso — mesmo tratamento de R0007 deste documento para os Pontos de Atributo. O mesmo vale para as regras de 2.c: o **GM não é bloqueado** pelo teto por elemento, pelo requisito de Sub-Elemento, pela visibilidade por Vocação/Classe nem pela recusa de baixar um requisito — a ficha **mostra** (elemento marcado, dica, contador vermelho) em vez de recusar. O **jogador** de um NPC concedido segue as regras do Personagem.

# **R0011** - O NPC também recebe Habilidades Passivas.

**Descrição**: A seção 4.f (Habilidades Passivas) segue "[[Requisitos - Ficha de Personagem]]" 4.f e R0008 sem alteração — ao contrário dos contadores de R0007 e R0010, aqui não há exceção para o GM. No NPC concedido a um jogador (R0001, exceção), o jogador só escolhe entre as Passivas públicas da campanha da concessão, e a concessão copia as Passivas do NPC de origem com Categoria e Requisitos.

  

# **R0012** - O VIS Atual do NPC é calculado, não editável.

**Descrição**: Como no Personagem (ver 1.b de "[[Requisitos - Ficha de Personagem]]"), o *VIS Atual* da Ficha de NPC é calculado e somente leitura; acompanha o *Nível* editado pelo GM (R0005) e os contadores de Âmbares. É esse valor que determina a *Graduação* (Grau/Círculo, segundo "[[Tabela de Circulo e Grau por EAP]]"), o máximo de Vocação Arcana e os Requisitos de Habilidades Passivas (R0011) — nenhum deles lê um número digitado à mão.

# **R0013** - Requisitos e Penalidades de equipamento seguem o Personagem.

**Descrição**: Os Requisitos e a Penalidade dos equipamentos (Arma, Armadura, Escudo e Artefato) funcionam na Ficha de NPC exatamente como na Ficha de Personagem (ver "[[Requisitos - Ficha de Personagem]]" 2.a e "[[Requisitos - Catálogo de Itens e Equipamentos]]" R0014).

# **R0014** — removido na 2.0.0

Coberto por "[[Requisitos - Ficha de Personagem]]" 4.b: o saldo de Pontos de Ignição vale para todos, inclusive o GM (ao contrário dos contadores de R0007 e R0010 deste documento).

# **R0015** - Condições ativas: só na ficha concedida.

**Descrição**: Um NPC **concedido** a um jogador segue "[[Requisitos - Ficha de Personagem]]" R0009: as Condições ativas dos participantes ligados a ele mudam os valores calculados da ficha, que o dono e o GM veem. Na ficha de um NPC **do GM** (não concedido) nada muda, por mais instâncias dele que estejam num encontro com Condições: elas só valem no resumo daquele participante no encontro, e a lista de Condições ativas da ficha fica vazia. O NPC não limita o Atual ao máximo ao salvar, então a exceção de R0009 não se aplica a ele.
