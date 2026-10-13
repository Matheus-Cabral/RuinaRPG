> Esta página não corresponde a nenhum bloco do "[[01 - Visão Geral.canvas]]" ainda — é uma ferramenta nova do GM, complementar a "[[Requisitos - Campanha]]".

  

> **Escopo**: o Encontro gerencia **estado** de combate (ordem de turno, recursos, ações, condições) — não rola dados nem resolve ataques/dano. As rolagens continuam sendo feitas fisicamente à mesa, como em todo o resto do sistema (ver "[[Requisitos - Ficha de Personagem]]" 2.d e 3.e).

  

> **Modelo de edição**: acesso 100% do GM, como o diário da campanha (ver "[[Requisitos - Campanha]]" R0005). Não há visualização de jogador para o Encontro.

  

# **R0001** - Um Encontro pertence a uma Campanha.

**Descrição**: O GM cria um Encontro de dentro de uma Campanha específica (ver "[[Requisitos - Campanha]]"). Uma Campanha pode ter vários Encontros; cada Encontro está sempre em uma de três fases: preparação, em andamento ou finalizado (ver R0007).

  

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

  

# **R0005** - Ações restantes resetam no início da vez de cada participante.

**Descrição**: Ao começar a vez de um participante (ver R0006), seu contador de Ações restantes volta automaticamente para **3**.

  

# **R0006** - O GM controla de quem é a vez manualmente.

**Descrição**: Os controles de vez só existem com o Encontro em andamento (ver R0007):

- **Próxima vez**: avança para o participante seguinte na ordem de Iniciativa (R0004), resetando as Ações restantes dele (R0005). Ao passar do último participante da lista, o Encontro inicia um novo **Turno** (contador incrementado) e volta ao primeiro participante da ordem.
- **Vez anterior**: volta um passo na ordem, inclusive do primeiro participante do Turno N para o último do Turno N−1, sem alterar as Ações restantes e sem voltar antes do início do Turno 1.
- **Passar a vez para este**, em cada participante: dá a vez ao participante escolhido, fora da ordem, mantendo o Turno e voltando as Ações restantes dele para 3.

A tela mostra sempre, em destaque, o Turno e de quem é a vez.

  

# **R0007** - O Encontro passa por três fases.

**Descrição**:

- **Preparação (Turno 0)**: o GM só monta os participantes e as Iniciativas; nada é registrado no log (R0010). Todo Encontro novo começa aqui.
- **Em andamento**: começa em "Iniciar encontro", que exige ao menos um participante, leva ao Turno 1 e dá a vez ao primeiro da ordem de Iniciativa (R0004), com 3 Ações restantes (R0005).
- **Finalizado**: "Finalizar encontro" (com confirmação) torna o Encontro somente leitura; "Reabrir" o devolve a em andamento, no mesmo Turno e na mesma vez.

Renomear o Encontro é permitido em qualquer fase. Encontros criados antes desta regra: os que nunca tiveram a vez avançada ficam no Turno 0 (preparação); os demais ficam em andamento, no Turno em que estavam, com o log começando a partir da atualização.

  

# **R0008** - O GM pode remover um participante.

**Descrição**: Na preparação e em andamento, o GM remove um participante do Encontro (com confirmação). Se ele tinha a vez, ela passa ao seguinte na ordem de Iniciativa (ou ao primeiro do Turno seguinte, se era o último). Se não sobrar nenhum participante, ninguém tem a vez até que um participante seja adicionado e o GM avance a vez.

  

# **R0009** - Encontros finalizados ficam em uma lista à parte.

**Descrição**: Na aba Encontros de "[[Requisitos - Campanha]]", a seção **Encontros** lista os Encontros em preparação e em andamento, cada um com a fase ("Preparação") ou o Turno ("Turno N"), e a seção **Encontros finalizados** lista os finalizados com a data de finalização, do mais recente para o mais antigo. Quando não há nenhum finalizado, a seção informa "Nenhum encontro finalizado.".

  

# **R0010** - O Encontro mantém um log a partir do Turno 1.

**Descrição**: Com o Encontro em andamento, são registrados:

- o início do Encontro, cada novo Turno e cada passagem de vez (próxima, anterior e escolhida);
- participante adicionado ou removido;
- alterações de Iniciativa, Ações restantes, PV/PF/PA e Condições de um participante;
- a finalização e a reabertura do Encontro;
- para um participante ligado a uma ficha (Personagem, ou NPC/Criatura concedido), as alterações de Vitalidade, Foco e Adrenalina feitas na própria ficha.

Nada é registrado na preparação nem depois da finalização. O log aparece na própria tela do Encontro, agrupado por Turno e, dentro dele, pela vez em que o evento ocorreu. Como o resto do Encontro, é restrito ao GM. Ver "[[Requisitos - Modelo de Dados]]" (EncounterLogEntries).
