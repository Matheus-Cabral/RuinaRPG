> Esta página corresponde aos encontros da ferramenta 5 (Campanhas) do PAINEL DO *GM* no "[[01 - Visão Geral]]" — uma ferramenta do GM complementar a "[[Requisitos - Campanha]]".

  

> **Escopo**: o Encontro gerencia **estado** de combate (ordem de turno, recursos, ações, condições) — não rola dados nem resolve ataques/dano. As rolagens continuam sendo feitas fisicamente à mesa, como em todo o resto do sistema (ver "[[Requisitos - Ficha de Personagem]]" 2.d e 3.e).

  

> **Modelo de edição**: acesso 100% do GM, como o diário da campanha (ver "[[Requisitos - Campanha]]" R0005). Não há visualização de jogador para o Encontro.

  

# **R0001** - Um Encontro pertence a uma Campanha.

**Descrição**: O GM cria um Encontro de dentro de uma Campanha específica (ver "[[Requisitos - Campanha]]"). Uma Campanha pode ter vários Encontros; cada Encontro está sempre em uma de três fases: preparação, em andamento ou finalizado (ver R0007).

  

# **R0002** - O GM adiciona participantes ao Encontro, um de cada vez.

**Descrição**: Para cada participante adicionado, o GM escolhe a origem:

- **Ficha de Personagem** de um jogador membro da campanha, incluindo Fichas de NPC/Criatura concedidas a ele (ver "[[Requisitos - Campanha]]" R0010).
- **Ficha de NPC ou de Criatura** do GM (não concedida a nenhum jogador). Pode ser adicionada mais de uma vez ao mesmo Encontro (ex: 3 goblins) — cada adição gera uma **instância** independente, sem duplicar a ficha original no Bestiário/NPCs do GM. Quando a mesma ficha é adicionada mais de uma vez, as instâncias depois da primeira recebem um número no nome ("Goblin", "Goblin 2", "Goblin 3"), usando o menor número a partir de 2 que não esteja em uso no Encontro.

  

# **R0003** - Cada participante tem Iniciativa, recursos, ações e condições.

**Descrição**: Todo participante do Encontro exibe:

- *Nome*: herdado da ficha de origem.
- *Iniciativa*: campo numérico, digitado manualmente pelo GM (o cálculo com Dado da Cena é feito à mesa, ver §5 de "[[Ruína RPG - Sistema Básico]]").
- *Vitalidade (PV) atual*, *Foco/Arcana (PF) atual* e *Adrenalina (PA) atual*: campos numéricos.
- *Ações restantes*: contador de 0 a 3 (ver §4 de "[[Ruína RPG - Sistema Básico]]").
- *Condições*: as Condições do catálogo aplicadas a ele (ver R0011), cada uma com ícone, nome e Turnos restantes quando tem duração. As tags de texto livre de antes do catálogo (ex: Envenenado, Enraizado) continuam exibidas, mas só podem ser removidas; condições novas vêm sempre do catálogo.

O comportamento de PV/PF/PA difere conforme a origem do participante (R0002):

- **Ficha de Personagem** (do jogador dono, incluindo pets/invocações concedidos): PV/PF/PA refletem em tempo real os valores salvos na ficha correspondente. O GM pode editá-los no Encontro, e a edição grava **na própria ficha**: cada valor fica no máximo que a ficha mostra com as Condições ativas (o mesmo limite do salvamento da ficha), e só o recurso que o GM de fato mudou é gravado — o que ele não tocou fica como está, então uma mudança simultânea do jogador não se perde. A alteração entra no log da ficha como feita pelo GM ("[[Requisitos - Campanha]]" R0016) e, com o Encontro em andamento, no log do Encontro (R0010), uma linha por recurso. A ficha aberta do dono e a do GM mostram os valores novos na hora, sem perder o campo em edição.
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

Com o Encontro em andamento, a tela mostra sempre, em destaque, o Turno e de quem é a vez. Na preparação ela mostra o Turno 0 e ninguém tem a vez.

  

# **R0007** - O Encontro passa por três fases.

**Descrição**:

- **Preparação (Turno 0)**: o GM só monta os participantes e as Iniciativas; nada é registrado no log (R0010). Todo Encontro novo começa aqui.
- **Em andamento**: começa em "Iniciar encontro", que exige ao menos um participante, leva ao Turno 1 e dá a vez ao primeiro da ordem de Iniciativa (R0004), com 3 Ações restantes (R0005).
- **Finalizado**: "Finalizar encontro" (com confirmação) torna o Encontro somente leitura; "Reabrir" o devolve a em andamento, no mesmo Turno e na mesma vez.

Renomear o Encontro é permitido em qualquer fase.

  

# **R0008** - O GM pode remover um participante.

**Descrição**: Na preparação e em andamento, o GM remove um participante do Encontro (com confirmação). Se ele tinha a vez, ela passa ao seguinte na ordem de Iniciativa (ou ao primeiro do Turno seguinte, se era o último; nesse caso o Turno avança em um). Quem recebe a vez fica com 3 Ações restantes (R0005). Se não sobrar nenhum participante, ninguém tem a vez até que um participante seja adicionado e o GM avance a vez.

  

# **R0009** - Encontros finalizados ficam em uma lista à parte.

**Descrição**: Na aba Encontros de "[[Requisitos - Campanha]]", a seção **Encontros** lista os Encontros em preparação e em andamento, cada um com a fase ("Preparação") ou o Turno ("Turno N"), e a seção **Encontros finalizados** lista os finalizados com a data de finalização, do mais recente para o mais antigo. Quando não há nenhum finalizado, a seção informa "Nenhum encontro finalizado.".

  

# **R0010** - O Encontro mantém um log a partir do Turno 1.

**Descrição**: Com o Encontro em andamento, são registrados:

- o início do Encontro, cada novo Turno e cada passagem de vez (próxima, anterior e escolhida);
- participante adicionado ou removido;
- alterações de Iniciativa, Ações restantes, PV/PF/PA e Condições de um participante;
- Condição aplicada, removida ou expirada (R0012) e cada ajuste de valor atual feito por uma Condição (R0013);
- a finalização e a reabertura do Encontro;
- para um participante ligado a uma ficha (Personagem, ou NPC/Criatura concedido), as alterações de Vitalidade, Foco e Adrenalina feitas na própria ficha.

Nada é registrado enquanto o Encontro está em preparação ou finalizado — a finalização e a reabertura em si são registradas. O log abre num **popup**, como o log de alterações da ficha (ver "[[Requisitos - Campanha]]" R0016), por um ícone de histórico "Log do encontro": na tela do Encontro (fora da preparação) e em cada Encontro das listas de Encontros e de Encontros finalizados da Campanha; também funciona, só para leitura, no Encontro finalizado. O popup agrupa as linhas por **dia** (no fuso do navegador, do mais novo ao mais antigo) e, dentro de cada dia, por Turno e, dentro dele, pela vez em que o evento ocorreu, na ordem em que aconteceram. Ele tem um filtro de **período** e duas caixas, ambas marcadas de início: "Vitalidade, Foco e Adrenalina" e "Estresse", que escondem só as linhas de alteração desses recursos; os eventos do Encontro (início, Turnos, vez, participantes, Condições) nunca são escondidos. O popup mostra o log como estava ao abrir. Como o resto do Encontro, é restrito ao GM. Ver "[[Requisitos - Modelo de Dados]]".

  

# **R0011** - O GM aplica Condições do catálogo aos participantes.

**Descrição**: Na preparação e em andamento, o GM aplica a um participante uma Condição do catálogo de "[[Requisitos - Auditoria de Regras]]" R0017. A Condição aplicada é uma **cópia independente**: editar ou excluir a entrada do catálogo depois não a altera. Ao aplicar, o GM pode:

- mudar a **duração** em Turnos (no mínimo 1), que começa com a duração padrão da entrada, ou marcar **sem duração** (mesmo que a entrada tenha uma padrão): a Condição então dura até ser removida;
- ajustar o **Valor** de cada modificador, com os mesmos limites da entrada no catálogo (nunca 0; um percentual fica entre −100 e 1000). Se a entrada mudou no catálogo enquanto o GM escolhia, a aplicação é recusada e ele reabre a escolha;
- escolher, em cada categoria desabilitada, se ela desabilita a categoria inteira ou só itens escolhidos da ficha do participante.
- restringir cada modificador de Maestria a Maestrias escolhidas da ficha do participante (só as da Perícia do modificador, quando ele tem uma); sem escolha, o modificador vale em todas, como na entrada. As Maestrias escolhidas aparecem pelo nome na explicação da Condição na ficha.

A mesma Condição não pode estar duas vezes no mesmo participante; Condições diferentes se somam. O GM remove uma Condição a qualquer momento antes de o Encontro ser finalizado.

Uma Condição só está **ativa** com o Encontro em andamento e enquanto a duração não venceu (R0012): na preparação ela fica no participante sem efeito nenhum, e começa a valer ao Iniciar. O efeito nos valores depende da origem do participante (R0002): numa Ficha de Personagem, ou numa Ficha de NPC/Criatura concedida a um jogador, a ficha mostra os valores com a Condição enquanto ela está ativa (ver "[[Requisitos - Ficha de Personagem]]" R0009), e o dono da ficha é avisado sempre que as Condições ativas dela mudam; numa Ficha de NPC/Criatura do GM, a ficha original nunca muda e a Condição só vale para aquela instância no Encontro, e o GM a vê no resumo da ficha (R0014).

  

# **R0012** - As Condições com duração expiram sozinhas.

**Descrição**: Uma Condição com duração **N** Turnos, aplicada no Turno **T**, vale do Turno T até o Turno T + N − 1 e é retirada do participante quando o Encontro **entra** no Turno T + N. Uma Condição aplicada na preparação conta a partir do Turno 1. Os Turnos restantes exibidos (R0003) seguem a mesma conta, então o contador e a expiração sempre concordam. A cada Turno novo, o dono de uma ficha (R0011) com uma Condição de prazo ativa é avisado, para o contador da ficha acompanhar.

A expiração acontece só ao entrar num Turno novo (pela **Próxima vez**, ao passar do último participante, ou ao remover o último participante da vez, R0008). **Vez anterior** nunca traz de volta uma Condição que expirou. Ao expirar, a ficha ligada ao participante volta aos valores sem ela, a expiração é registrada no log do Encontro (R0010) e no log da ficha como automática, e o dono da ficha é avisado.

  

# **R0013** - Os ajustes de valor atual das Condições acontecem na hora certa e uma única vez.

**Descrição**: Um modificador de **valor atual** de recurso (ver "[[Requisitos - Auditoria de Regras]]" R0017) muda de verdade o valor corrente, e esse ajuste nunca é devolvido — nem ao remover a Condição, nem ao voltar a vez.

- **Ao aplicar**: roda uma única vez, na primeira vez em que a Condição fica ativa — na hora, se o Encontro já está em andamento, ou ao **Iniciar**, se ela foi aplicada na preparação. Uma Condição aplicada e removida ainda na preparação nunca roda.
- **A cada vez do participante**: roda cada vez que o participante **recebe** a vez — por **Próxima vez**, por **Passar a vez para este**, ao **Iniciar** (o primeiro da ordem) e quando a vez passa a ele porque quem a tinha foi removido (R0008) —, no máximo **uma vez por Turno** para cada modificador. **Vez anterior** não roda nem desfaz nada; voltar e avançar de novo no mesmo Turno não repete o ajuste, e o Turno seguinte roda de novo.
- Quando uma mesma ação entra num Turno novo e dá a vez a alguém, primeiro expiram as Condições vencidas (R0012), depois rodam os ajustes das que continuam ativas.

**Onde o ajuste cai**:

- **Ficha de Personagem**, ou **Ficha de NPC/Criatura concedida**: no valor atual guardado na ficha, que fica entre 0 e o máximo da ficha com as Condições ativas (de todos os Encontros em andamento de que ela participa). O ajuste aparece no log da ficha como automático e o dono da ficha é avisado.
- **Ficha de NPC/Criatura do GM**: no PV/PF/PA daquela instância no Encontro, que nunca fica abaixo de 0 e não tem teto (como quando o GM os edita à mão, R0003). Estresse não é acompanhado no Encontro: o ajuste é pulado.

Um modificador **percentual** é um percentual do **total** do recurso, arredondado para baixo: o máximo da ficha com as Condições ativas; numa instância do GM, o máximo da ficha original calculado com as Condições **daquela instância**. Se a ficha original já não existe, o percentual é pulado (um fixo ainda roda).

**Log**: cada ajuste vira uma linha no log do Encontro (R0010) com o participante, o recurso, o valor antes e depois e o nome da Condição; um ajuste pulado também vira uma linha, dizendo por quê ("Estresse não é acompanhado no encontro" ou "ficha de origem removida"). O ajuste na ficha não gera, além disso, a linha de alteração de recurso da ficha.

**Ações simultâneas**: duas ações do GM que poderiam rodar o mesmo ajuste ao mesmo tempo (ex: um clique duplo em **Passar a vez para este**, ou aplicar uma Condição enquanto a vez avança) nunca o aplicam duas vezes: uma delas é recusada com o aviso de que o Encontro foi alterado por outra ação, e o GM recarrega e tenta de novo.

  

# **R0014** - O GM vê o resumo da ficha de um NPC ou Criatura com as Condições aplicadas.

**Descrição**: Cada linha de participante que veio de uma Ficha de NPC ou de Criatura (R0002) tem um botão de **Resumo da ficha** (ícone de olho). Ele abre um popup **somente leitura**, só para o GM do Encontro, que mostra a ficha daquele participante com as Condições ativas dele (R0011) já aplicadas. Para um participante de Ficha de Personagem não há botão (o GM e o jogador já veem a ficha com as Condições, ver "[[Requisitos - Ficha de Personagem]]" R0009). O popup funciona também num Encontro finalizado, mas então não há Condição ativa (R0011) e os valores são os da ficha.

**Conteúdo**, na ordem e com os rótulos das seções da ficha ("[[Requisitos - Ficha de NPCs]]", "[[Requisitos - Ficha de Criaturas]]"):

- imagem, nome, Nível e, na Criatura, o Rank;
- **Atributos**, **Sub-Atributos** e defesas;
- **Recursos**: Vitalidade, Foco e Adrenalina e, no NPC, Estresse, atual e total (o Estresse atual é o da ficha: o Encontro não o acompanha);
- **Perícias**: só as que têm pontos gastos e as que uma Condição alterou;
- **Equipamento em uso** (armas e escudos equipados, armaduras e artefatos) e **Magias e Habilidades** (com Passivas e, no NPC, Runas);
- **Características**;
- as **Condições ativas**, cada uma com o que ela muda e os Turnos restantes.

Uma linha de equipamento ou de magia que uma Condição desabilita (R0011) aparece com o aviso "Desabilitado por: {Condição}".

**Valores com e sem Condição**: os números são os mesmos das telas da ficha. Um número que a Condição mudou aparece destacado, com o valor original riscado ao lado; os demais aparecem normais.

**Quais Condições valem**:

- **Ficha do GM** (sem dono): as Condições **daquele participante**. Duas instâncias da mesma ficha no Encontro mostram números diferentes se têm Condições diferentes, e a ficha original, em todas as suas telas, continua sem Condição nenhuma. O Atual de cada recurso é o PV/PF/PA da instância (R0003); o total é o da ficha original calculado com as Condições da instância.
- **Ficha concedida** a um jogador: as Condições **da ficha** — as mesmas que o dono e o GM veem na própria ficha, o que inclui as de outro Encontro em andamento de que ela também participa. Assim o resumo nunca difere da ficha. O Atual é o da ficha.

**Ficha removida**: se a ficha de origem já não existe, o resumo não está disponível e o popup avisa isso. O resumo apenas lê: não grava nada na ficha, no participante nem no log. Há um atalho para abrir a **ficha completa** numa aba nova.
