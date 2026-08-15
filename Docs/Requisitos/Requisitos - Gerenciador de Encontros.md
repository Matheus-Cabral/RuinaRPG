> Esta página não corresponde a nenhum bloco do "[[01 - Visão Geral.canvas]]" ainda — é uma ferramenta nova do GM, complementar a "[[Requisitos - Campanha]]".

  

> **Escopo**: o Encontro gerencia **estado** de combate (ordem de turno, recursos, ações, condições) — não rola dados nem resolve ataques/dano. As rolagens continuam sendo feitas fisicamente à mesa, como em todo o resto do sistema (ver "[[Requisitos - Ficha de Personagem]]" 2.d e 3.e).

  

> **Modelo de edição**: acesso 100% do GM, como o diário da campanha (ver "[[Requisitos - Campanha]]" R0005). Não há visualização de jogador para o Encontro.

  

# **R0001** - Um Encontro pertence a uma Campanha.

**Descrição**: O GM cria um Encontro de dentro de uma Campanha específica (ver "[[Requisitos - Campanha]]"). Uma Campanha pode ter vários Encontros, um de cada vez ativo ou não.

  

# **R0002** - O GM adiciona participantes ao Encontro, um de cada vez.

**Descrição**: Para cada participante adicionado, o GM escolhe a origem:

- **Ficha de Personagem** de um jogador membro da campanha, incluindo Fichas de NPC/Criatura concedidas a ele (ver "[[Requisitos - Campanha]]" R0010).
- **Ficha de NPC ou de Criatura** do GM (não concedida a nenhum jogador). Pode ser adicionada mais de uma vez ao mesmo Encontro (ex: 3 goblins) — cada adição gera uma **instância** independente, sem duplicar a ficha original no Bestiário/NPCs do GM.

  

# **R0003** - Cada participante tem Iniciativa, recursos, ações e condições.

**Descrição**: Todo participante do Encontro exibe:

- *Nome*: herdado da ficha de origem.
- *Iniciativa*: campo numérico, digitado manualmente pelo GM (o cálculo com Dado da Cena é feito à mesa, ver §5 de "[[Ruína RPG - Sistema Básico]]").
- *Vitalidade (PV) atual*, *Foco/Arcana (PF) atual* e *Adrenalina (PA) atual*: campos numéricos.
- *Ações restantes*: contador de 0 a 3 (ver §4 de "[[Ruína RPG - Sistema Básico]]").
- *Condições*: lista de tags em texto livre (ex: Envenenado, Enraizado — não há catálogo estruturado de condições ainda).

O comportamento de PV/PF/PA difere conforme a origem do participante (R0002):

- **Ficha de Personagem** (do jogador dono, incluindo pets/invocações concedidos): PV/PF/PA são **somente leitura** na tela do Encontro, refletindo em tempo real os valores salvos na ficha correspondente — o jogador atualiza a própria ficha durante o jogo, e o Encontro só exibe.
- **Ficha de NPC/Criatura do GM**: PV/PF/PA são uma **cópia independente** daquela instância, iniciando nos valores atuais da ficha original no momento em que foi adicionada ao Encontro, e editável livremente pelo GM ali. Alterações feitas no Encontro não se propagam de volta para a ficha original, e vice-versa.

  

# **R0004** - Os participantes são ordenados por Iniciativa.

**Descrição**: A lista de participantes é ordenada da maior para a menor Iniciativa (R0003) — quem tem Iniciativa maior fica na frente da fila de turnos.

  

# **R0005** - Ações restantes resetam no início do turno de cada participante.

**Descrição**: Ao começar o turno de um participante (ver R0006), seu contador de Ações restantes volta automaticamente para **3**.

  

# **R0006** - O GM avança os turnos manualmente.

**Descrição**: Um controle "Próximo turno" avança para o participante seguinte na ordem de Iniciativa (R0004), resetando as Ações restantes dele (R0005). Ao passar do último participante da lista, o Encontro inicia uma nova **Rodada** (contador incrementado) e volta ao primeiro participante da ordem.
