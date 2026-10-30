> Esta página corresponde à ferramenta 9 do PAINEL DO *GM* no "[[01 - Visão Geral]]".

  

> **Modelo de acesso**: pertence a um único usuário por vez na prática, designado fora do app — um administrador do servidor roda um comando `make` que marca uma conta de GM existente como Auditor de Regras. Nenhum jogador, e nenhum GM que não tenha sido designado assim, alcança as páginas deste documento.

  

# **R0001** - O Auditor de Regras é designado por e-mail, via comando `make` no console do servidor.

**Descrição**: `make grant-rules-auditor EMAIL=...` marca a conta de GM com aquele e-mail (comparação sem diferenciar maiúsculas/minúsculas) como Auditor de Regras; `make revoke-rules-auditor EMAIL=...` desfaz. Só uma conta de **GM** pode ser concedida (uma tentativa contra um e-mail de Jogador, ou um e-mail inexistente, falha com uma mensagem clara no log do servidor); revogar não tem essa restrição. A concessão e a revogação têm efeito imediato, sem o Auditor precisar sair e entrar de novo.

  

# **R0002** - O Auditor de Regras pode editar o texto do Livro de Regras.

**Descrição**: Uma página lista os 3 documentos do "[[Requisitos - Livro de Regras]]" que não são a aba de Características nem a Tabela de Níveis (esta é montada pela tabela de R0013) — Sistema Básico, Graus & Círculos, As Estrelas Alkerianas — cada um com o Markdown atual (a sobrescrita salva, ou o texto padrão do arquivo-fonte) em um campo de texto editável, e um botão "Restaurar padrão" que apaga a sobrescrita. A página exibe um aviso fixo: editar aqui só muda o texto exibido nesta página — nenhuma fórmula ou tabela usada nos cálculos das fichas (Vitalidade, Foco, Graduação, XP por nível, etc.) é afetada, essas continuam fixas no sistema.

  

# **R0003** - O Auditor de Regras tem CRUD completo sobre o catálogo de Características.

**Descrição**: Uma página separada lista todas as Características (Positivas e Negativas, ver "[[Características]]"), cada uma com Nome, Custo, Polaridade, Descrição e a flag "Exige Especificação?" editáveis, mais um botão de exclusão por linha e um formulário para adicionar uma nova. O sinal do Custo deve ser consistente com a Polaridade (Positiva ≥ 0, Negativa ≤ 0) — uma tentativa de salvar um valor inconsistente é rejeitada. Excluir uma característica já usada em alguma Ficha de Personagem/NPC é rejeitado, com uma mensagem indicando o conflito.

Diferente do Catálogo de Itens ou do Banco de Magias, esse catálogo não é por GM — a mudança vale para o servidor inteiro, todo GM e jogador vê o resultado. Editar ou excluir uma característica originalmente vinda do documento-fonte não é desfeito por uma futura atualização/reinicialização do servidor.

  

# **R0004** - A aba "Características" do Livro de Regras reflete esse catálogo em tempo real.

**Descrição**: Ao contrário dos outros 3 documentos (R0002), a aba de Características do "[[Requisitos - Livro de Regras]]" não tem uma sobrescrita de texto própria — ela é montada diretamente a partir do catálogo de R0003. Uma edição salva em R0003 aparece nessa aba imediatamente, sem precisar de nenhuma ação adicional do Auditor.

  

# **R0005** - O Auditor de Regras tem CRUD completo sobre um catálogo separado de características exclusivas de Criatura.

**Descrição**: Uma página separada de R0003 (mesmo formato: Nome, Custo, Polaridade, Descrição, "Exige Especificação?", exclusão por linha, formulário de adicionar, mesma consistência de sinal do Custo, mesmo bloqueio de exclusão em uso) lista um catálogo à parte — **não é a mesma tabela de R0003**. Só a Ficha de Criatura pode conceder uma característica desse catálogo, e ela só concede as deste catálogo (ver "[[Requisitos - Ficha de Criaturas]]" 5.d, que também descreve o atalho de cadastro do Auditor na própria ficha); Personagem, NPC e a aba "Características" do "[[Requisitos - Livro de Regras]]" nunca têm acesso a ele. Assim como R0003, é global (vale para o servidor inteiro, não por GM).

# **R0006** - O Auditor de Regras tem CRUD sobre o catálogo de Efeitos de "[[GRAUS & CÍRCULOS]]".

**Descrição**: Uma página separada lista, agrupados por Grau/Círculo (de 1 a 9 — um Grau fora dessa faixa é recusado), todos os Efeitos que uma Magia/Habilidade pode comprar — Nome, Tipo de Custo (Fixo, Por Unidade, Manual, Manual por Unidade, ou Derivado de outro Efeito), o valor de custo correspondente, o rótulo da unidade (quando houver Quantidade), o teto de Quantidade (quando houver, podendo escalar com o Grau/Círculo da Magia/Habilidade), e os grupos de pré-requisito (um Efeito pode exigir que um ou mais outros já estejam na mesma Magia/Habilidade, incluindo grupos alternativos "ou"). O catálogo nasce de um seed inicial extraído de "[[GRAUS & CÍRCULOS]]" — quando o próprio texto do sistema deixa um Efeito sem definição (ex.: "Selar", citado como pré-requisito alternativo do Detrito mas nunca definido), o Auditor cadastra a entrada faltante diretamente aqui. Assim como o catálogo de Características (R0003), é global (vale para o servidor inteiro, não por GM) e uma edição não é desfeita por uma futura atualização/reinicialização do servidor.

Criar, editar ou excluir um Efeito aqui também atualiza o bloco desse Efeito (`## Nome`) no documento "[[GRAUS & CÍRCULOS]]" do Livro de Regras (R0002), na mesma gravação: criar insere o bloco ao fim da seção do seu Grau/Círculo (se ainda não houver um bloco com esse nome); editar reescreve o bloco — movendo-o de seção se o Grau/Círculo mudou e renomeando o título se o Nome mudou; excluir remove o bloco. O bloco de um Efeito é o `## Título` cujo texto é igual ao Nome, sem diferenciar maiúsculas/minúsculas nem acentos (por isso o catálogo também recusa um Nome que só difira de outro nisso); as únicas exceções são os blocos compartilhados — "Efeitos Básicos" (exatamente Dano, Alcance e Duração) e "Libra" (as variantes `Libra (…)`) —, que nunca são reescritos nem removidos por essa sincronização. O Livro sempre contém todo Efeito do catálogo: a cada atualização do servidor (make migrate) e ao "Restaurar padrão" desse documento, todo Efeito ausente do texto é acrescentado (sem reescrever nem remover blocos existentes). Como essa verificação só acrescenta, "Restaurar padrão" também traz de volta os blocos originais de Efeitos que tenham sido renomeados ou excluídos no catálogo.

# **R0007** - O Custo em PI de cada Efeito numa Magia/Habilidade é calculado automaticamente a partir do catálogo de R0006.

**Descrição**: Em toda tela que adiciona um Efeito a uma Magia/Habilidade (Banco de Magias e as 3 Fichas), o Nome do Efeito é escolhido de uma lista restrita aos Efeitos cujo Grau já foi desbloqueado pelo Grau/Círculo da própria Magia/Habilidade (acesso cumulativo: um Efeito de Grau 2 continua disponível numa Magia de Grau 5) e cujos pré-requisitos já estão presentes na mesma lista de Efeitos. O Custo em PI é somente-leitura, calculado a partir do Tipo de Custo do Efeito escolhido — exceto os tipos Manual/Manual por Unidade, onde o próprio livro de regras deixa o valor a critério do Mestre, e que por isso ganham um campo numérico para essa entrada manual. Uma submissão cujo Custo em PI não bata com o valor recalculado, cujo Grau não esteja desbloqueado ou cujos pré-requisitos não estejam satisfeitos é recusada.

  

# **R0008** - O Auditor de Regras tem CRUD completo sobre o catálogo de Históricos.

**Descrição**: Uma página separada lista todos os Históricos (ver "[[Historico]]"), cada um com Nome, Descrição e as duas Perícias bonificadas (+6 e +3) editáveis, mais um botão de exclusão por linha e um formulário para adicionar um novo. As duas Perícias de uma mesma entrada não podem ser iguais — uma tentativa de salvar as duas iguais é rejeitada. Excluir um Histórico já usado em alguma Ficha de Personagem/NPC é rejeitado, com uma mensagem indicando o conflito.

Assim como o catálogo de Características (R0003), esse catálogo não é por GM — a mudança vale para o servidor inteiro. Editar ou excluir um Histórico originalmente vindo do documento-fonte não é desfeito por uma futura atualização/reinicialização do servidor. Renomear um Histórico já seedado não impede que o nome original volte a ser criado como uma nova entrada na próxima reinicialização do servidor — o Auditor deve excluir manualmente a entrada duplicada caso isso aconteça.

# **R0009** - O Auditor de Regras tem CRUD completo sobre o catálogo de kits de Equipagem inicial.

**Descrição**: Uma página separada lista todos os kits de Equipagem (ver "[[Requisitos - Ficha de Personagem]]"), cada um com Nome, Descrição e Ciclos editáveis, mais um formulário para adicionar um novo e um botão de exclusão por kit. Dentro de cada kit, duas sub-tabelas com CRUD próprio: os itens fixos que todo personagem que escolhe o kit recebe, e os slots de escolha (Label, Tipo, Subcategorias permitidas, Rank, Quantidade) que o jogador preenche ao escolher o kit — cada uma com seu próprio formulário de adicionar e exclusão por linha.

Um item fixo **é escolhido da base de itens fixos** (R0014), não digitado: a linha de adicionar tem um seletor de Tipo (Item Geral, Arma, Armadura, Escudo ou Artefato), um campo de pesquisa por nome sobre a base daquele Tipo, a Quantidade e o botão "Adicionar item". Quando a pesquisa não acha nada (e há texto digitado), aparece ao lado do campo o botão "Cadastrar novo item", que abre o formulário completo de R0014 já com o Tipo e o nome digitado e seleciona o item criado. **Armadura é aceita** como item fixo, e então exige o Slot de Armadura (Capacete, Superior ou Inferior). A tabela mostra, por linha, Nome, Tipo (com o Slot entre parênteses para Armadura), Quantidade (editável na própria linha; nas linhas de Armadura, o Slot também é editável na própria linha, por um seletor Capacete/Superior/Inferior) e duas ações: excluir e editar. Editar abre um popup com duas opções: **"Editar detalhes"**, que altera o item da base (e portanto vale para todos os kits que o usam; o popup avisa quais são) e **"Substituir item"**, que troca a linha por outro item da base, mantendo a Quantidade (pedindo o Slot quando o novo item é uma Armadura). Uma linha cujo item fixo está incompleto (ver conversão em R0014) mostra a marca **"Detalhes incompletos"**. Uma alteração de um kit (adicionar, editar a Quantidade, substituir, excluir) que for recusada mostra o motivo. Excluir um kit já escolhido em alguma Ficha de Personagem/NPC é rejeitado, com uma mensagem indicando o conflito.

Assim como os catálogos de Características (R0003) e Históricos (R0008), esse catálogo não é por GM — a mudança vale para o servidor inteiro. Renomear um kit já seedado não impede que o nome original volte a ser criado como uma nova entrada na próxima reinicialização do servidor (mesma limitação de R0008) — o Auditor deve excluir manualmente a entrada duplicada caso isso aconteça.

# **R0010** - A mesma página gerencia o vocabulário de Categoria/Família usado pelo Construtor de Subcategoria, e os slots de escolha aceitam Armadura/Escudo/Artefato além de Arma.

**Descrição**: Uma seção "Construtor de Subcategoria", na mesma página de R0009, lista — por Tipo (Arma, Armadura, Escudo ou Artefato, escolhido num seletor) — duas listas independentes de valores: Categorias e Famílias. Cada lista tem seu próprio formulário de adicionar e exclusão por linha; não há edição — renomear um valor é excluir e adicionar de novo. Um valor é aparado de espaços nas pontas e não pode ficar vazio, começar ou terminar com `-`, nem conter `,` ou a sequência `" - "` (ver "[[Requisitos - Catálogo de Itens e Equipamentos]]" R0013); a exclusão é lógica — um item do catálogo já montado com aquele valor mantém sua string composta intacta. Esse vocabulário é global (vale para o servidor inteiro, não por GM), como os demais catálogos deste documento.

Os slots de escolha de um kit de Equipagem (a sub-tabela de R0009) passam a aceitar Tipo Arma, Armadura, Escudo ou Artefato. Um slot de Tipo Armadura exige também um Slot de Armadura (Capacete, Superior ou Inferior), indicando qual posição da ficha aquela escolha preenche; os demais Tipos não têm esse campo. As "Subcategorias" permitidas do slot passam a ser escolhidas entre os valores de Família cadastrados aqui para o Tipo do slot (um multi-seletor) — vazio continua significando "qualquer". Um item fixo do tipo Armadura e um slot de escolha de Armadura do mesmo kit não podem ter o mesmo Slot de Armadura (nem duas Armaduras fixas, ou dois slots de Armadura, o mesmo Slot).

# **R0011** - O Auditor de Regras edita a Durabilidade por Rank.

**Descrição**: Uma página separada lista as 8 linhas fixas da "[[Tabela de Durabilidade por Rank]]" — uma por Rank de item (F, E, D, C, B, A, S, SS) — cada uma com um número (Durabilidade) ou a marcação "Inquebrável" editáveis; não há criação nem exclusão de linha. Marcar "Inquebrável" limpa o número daquela linha (a durabilidade máxima passa a não existir); desmarcar exige informar um número (mínimo 1). Os valores iniciais vêm do documento-fonte (F 20, E 45, D 80, C 125, B 180, A 245, S e SS Inquebrável).

Essa tabela é o que resolve a durabilidade máxima de toda Arma, Armadura e Escudo em toda Ficha (Personagem, NPC, Criatura) e no Catálogo de Itens — um item sem Rank não tem durabilidade. Reduzir o valor de uma linha limita (mas não recarrega) a durabilidade atual das fichas que já ultrapassam o novo máximo; aumentar o valor não recarrega a durabilidade atual de ninguém. Diferente dos demais catálogos deste documento, essa tabela não aparece no "[[Requisitos - Livro de Regras]]".

# **R0012** - O Auditor mantém a lista de Perícias.

**Descrição**: A página **Auditoria → Perícias** lista todas as perícias do sistema. Cada perícia tem **Nome** (obrigatório, único entre as ativas), **Descrição** (opcional), **Atributo sugerido** (opcional) e **Disponível para Criaturas**. O Auditor pode:

- **Adicionar** uma perícia: ela aparece em todas as fichas (Personagem, NPC e, se marcada, Criatura) com 0 pontos.
- **Editar** qualquer campo. O Atributo sugerido só vem pré-selecionado numa ficha — o jogador sempre pode escolher outro atributo, porque o GM pode pedir a perícia com outro atributo numa ação específica.
- **Remover** uma perícia: a remoção é lógica. Os pontos gastos nela são **devolvidos** ao saldo de Pontos de Perícia de todas as fichas, e enquanto removida ela some das fichas e das listas de escolha; bônus de Histórico, requisitos de Passiva e Maestrias ligados a ela deixam de valer.
- **Restaurar** uma perícia removida: ela volta com 0 pontos em todas as fichas. Recusado se já houver uma perícia ativa com o mesmo nome.

**Prontidão**, **Reflexos** e **Fortitude** entram em fórmulas (ver "[[Formulas]]"): podem ser renomeadas e descritas, mas não removidas nem retiradas das Criaturas. Na ficha, a Descrição aparece ao passar o mouse sobre o nome da perícia (desktop) ou ao tocar nele (celular).

# **R0013** - O Auditor edita a Tabela de Níveis.

**Descrição**: A página **Auditoria → Tabela de Níveis** mostra uma linha por nível e uma coluna por recurso, mais um campo de texto livre **Outros bônus** por nível (ex.: "Status de Vida Aprimorado", "Primeira Passiva"). Cada coluna é de um de dois tipos:

- **Acumulativa**: o valor da ficha é a soma dos níveis 1 até o nível atual (célula vazia vale 0). Ex.: Pontos de Atributo, Pontos de Perícia, Espaços de Característica, Pontos de Ignição, Espaços e Pontos de Maestria. Também são Acumulativas as quatro colunas de Passivas, **Passivas Livres**, **Passivas Vocacionais**, **Passivas De Classe** e **Passivas Coringa**: o Auditor escreve `1` apenas nos níveis que concedem uma Passiva daquela categoria, e o limite da ficha é a soma da coluna do nível 1 até o nível dela (abaixo do primeiro nível que concede, o limite é 0). Uma coluna de Livres, Vocacionais ou De Classe **inteiramente vazia** significa sem limite para a categoria. A quarta coluna, **Passivas Coringa**, também Acumulativa, concede vagas que aceitam uma Passiva de **qualquer** categoria; vazia, vale 0 vagas (não "sem limite"). Como são Acumulativas, o aviso de subida de nível mostra "Passiva Livre: +1" nos níveis que concedem.
- **Por nível**: vale o número do nível atual; uma célula vazia repete o valor do nível anterior mais próximo, e sem nenhum valor não há limite. Ex.: Máx. de Atributo, Máx. de Perícia. **XP para o próximo nível** e **EAP base** também são Por nível, mas nunca repetem valor: XP vazio no último nível significa nível máximo. O XP do último nível é sempre ignorado, mesmo preenchido: o último nível é o máximo, não existe um nível seguinte (para liberar um novo nível, adicione-o à tabela e preencha o XP do nível anterior).

**Nomes das colunas de Passiva**: fora das páginas de Auditoria, as quatro colunas têm rótulo fixo — "Habilidade Passiva" (Coringa), "Passiva Livre", "Passiva Vocacional" e "Passiva de Classe" — no painel Progressão do nível, no aviso de subida de nível e na aba Tabela de Níveis do Livro de Regras. Renomear uma dessas colunas só muda a grade da Auditoria.

**Colunas de sistema novas**: uma coluna de sistema acrescentada por uma versão nova do app entra, numa tabela já preenchida, logo depois da coluna de sistema anterior; as seguintes são deslocadas.

As colunas acima são do sistema — podem ser renomeadas, não removidas, e alimentam as fichas. O Auditor pode criar colunas próprias (nome + tipo, o tipo não muda depois), que aparecem na ficha só como informação, e removê-las (a remoção pede confirmação e apaga os valores da coluna). Pode também adicionar um nível ao fim da tabela e remover o último nível, desde que nenhuma ficha esteja nele.

**Limites**: Máx. de Atributo e Máx. de Perícia limitam os pontos gastos (Gasto) num atributo ou numa perícia; as colunas de Passivas (Livres, Vocacionais, De Classe), pela soma aditiva descrita acima, limitam quantas Passivas de cada categoria a ficha pode ter. O que passa do limite de uma categoria ocupa vagas coringa (coluna Passivas Coringa, acumulada): a adição é recusada quando a categoria está cheia e todas as vagas coringa já estão em uso. Uma categoria de coluna inteiramente vazia segue sem limite e não ocupa vaga coringa. Nenhuma Passiva é marcada como ocupante de uma vaga coringa; a conta é feita sempre sobre o total da ficha. Valem para Personagem e NPC: o servidor recusa salvar acima do limite, mas uma ficha que já está acima continua válida e pode baixar o valor. A Criatura não tem limites, mas o Nível de NPC e Criatura é validado entre 1 e o último nível da tabela. Níveis acima do último das tabelas de Vocação, Classe e Arquétipo (que continuam fixas, com 50 níveis) usam os valores de Vida e Arcana do último nível dessas tabelas. O aviso de subida de nível lista os bônus numéricos como "<Nome da coluna>: +N" (as quatro de Passiva, com o rótulo fixo), seguidos das linhas de Outros bônus.

O Livro de Regras monta a aba Tabela de Níveis a partir desta tabela; ela aparece como uma tabela "Nível | Bônus" (o que cada nível concede, um item por linha: colunas Acumulativas e Outros bônus; "—" quando nada) e, só quando o Auditor marca a opção "Mostrar a tabela "Limites e progressão" no Livro de Regras" (checkbox acima da grade, desmarcada por padrão, com um popup ⓘ explicativo; salva ao marcar/desmarcar, e uma falha desfaz a marcação e mostra o motivo), abaixo dela, "Limites e progressão" com as colunas Por nível que têm algum valor (ver "[[Requisitos - Livro de Regras]]"; a opção vale para o servidor inteiro). O Compêndio continua indexando os arquivos Markdown. A tabela é preenchida uma única vez, a partir de "[[Tabela de Níveis]]" e "[[Tabelas de XP, Atributos, Características e EAP]]", quando está vazia; depois disso, editar esses arquivos não altera orçamentos nem limites.

# **R0014** - O Auditor mantém a base de itens fixos dos kits.

**Descrição**: Os itens fixos dos kits (R0009) vêm de uma **base global**, mantida pelo Auditor de Regras e igual para todas as campanhas. Cada item da base é **completo**: tem os mesmos campos do formulário do catálogo de "[[Requisitos - Catálogo de Itens e Equipamentos]]" por Tipo — inclusive Requisitos e Penalidade de requisitos — mas **sem imagem**. É único por **Nome + Tipo**; tentar repetir o par é recusado. O Tipo de um item da base não pode ser alterado depois de criado.

Na mesma página de R0009, uma seção **"Itens fixos"** lista a base com filtro por Tipo e pesquisa por nome, mostrando Nome, Tipo e os kits que usam cada item, com um botão "Novo item fixo", editar e excluir por linha. O mesmo formulário é aberto por "Cadastrar novo item" (R0009). **Editar** um item vale para todos os kits que o usam: o formulário lista esses kits e avisa. **Excluir** é recusado enquanto um kit (não excluído) usa o item, com uma mensagem que nomeia os kits. O **bônus condicional** de um slot de escolha (por exemplo, as flechas ao escolher um arco) também é um item da base, obrigatoriamente do tipo Item Geral, informado junto com a Subcategoria que o ativa e uma Quantidade de pelo menos 1.

**Itens incompletos**: um item da base que só tem nome (vindo da conversão automática dos kits antigos) é marcado **"Detalhes incompletos"** até o Auditor preenchê-lo. Ao aplicar um kit, os itens fixos viram cópias no catálogo do GM, como descrito em "[[Requisitos - Catálogo de Itens e Equipamentos]]" R0012.

# **R0015** - O Auditor vê as novidades das páginas de Auditoria no diálogo de versão.

**Descrição**: Ao entrar depois de uma atualização, todo GM vê o diálogo "Novidades da Versão" (Jogadores não recebem o diálogo de versão). Além da lista geral, o diálogo tem um bloco **"Para Auditores"**, exibido **só** a um Auditor de Regras e **depois** da lista geral, que descreve o que mudou nas páginas de Auditoria naquela versão. Nas versões que não mexem em nenhuma página de Auditoria, o bloco é omitido.

# **R0016** - O Auditor mantém a Tabela de Afinidades.

**Descrição**: Uma página própria de Auditoria (separada das demais) mantém a "[[Tabela de Afinidades]]", que define a Eficiência Elemental e o Dano Elemental das fichas. Cada linha tem uma **Afinidade** (única na tabela), uma **Eficiência** e um **Dano**, todos inteiros maiores ou iguais a 0. O Auditor pode criar, editar e excluir linhas; repetir uma Afinidade é recusado.

**Regra de consulta**: dado o Valor da Afinidade da ficha (os pontos que ela tem no elemento escolhido como *Afinidade*, ver "[[Requisitos - Ficha de Personagem]]" 2.b e 2.c), vale a linha de **maior Afinidade que não ultrapassa** esse Valor. Abaixo da menor linha, ou com a tabela vazia, Eficiência e Dano valem 0; acima da maior linha, valem os valores dela. A mudança vale imediatamente para toda Ficha de Personagem e de NPC (a Criatura não tem esses valores; ver "[[Requisitos - Ficha de Personagem]]" 2.b).

Os valores iniciais vêm de "[[Tabela de Afinidades]]" e são semeados **só enquanto a tabela está vazia**; depois disso vale o que o Auditor mantém, e editar o Markdown não altera mais a tabela. A Tabela de Afinidades não aparece no "[[Requisitos - Livro de Regras]]".

# **R0017** - O Auditor mantém o catálogo de Condições.

**Descrição**: A página **Auditoria → Condições** mantém o catálogo das **Condições** de combate (estados como Envenenado, Lento ou Abençoado que o Mestre aplica a participantes de um encontro). O catálogo é **global**, igual para todas as campanhas, e **só o Auditor** cria, edita e exclui; qualquer usuário autenticado pode consultá-lo (o Mestre precisa da lista para aplicar). Ele **não é** o catálogo de Efeitos de R0006, que são os Efeitos que uma Magia/Habilidade compra com PI. A página começa vazia: nenhuma Condição é semeada.

**Campos**: **Nome** (obrigatório, único entre as não excluídas), **Descrição**, **Ícone** (uma escolha entre uma lista fixa de cerca de cem ícones de RPG — elementos, estados físicos e mentais, sentidos, bônus, penalidades, magia —, cada um com nome em português; a grade fica num painel recolhido "Ícone", cujo cabeçalho mostra o ícone escolhido), **Duração padrão** em Turnos (opcional; se informada, no mínimo 1; vazia = dura até ser removida) e as listas de **modificadores** e de **categorias desabilitadas**. Uma Condição precisa de pelo menos um modificador ou uma categoria desabilitada. Na lista, cada Condição mostra o ícone, o nome, uma linha com o que ela faz, a duração padrão e os botões de editar e excluir; o formulário, com a tabela de modificadores, abre num painel acima da lista, e excluir pede confirmação.

**Modificadores**: cada linha escolhe um **Alvo** e, conforme ele, o que exatamente muda:

- **Atributo**: um dos oito atributos (obrigatório).
- **Perícia**: todas as Perícias ou uma Perícia.
- **Sub-atributo**: Iniciativa, Movimentação, Esquiva Natural, Defesa Natural, Redução Física, Redução Mágica ou Adrenalina (obrigatório; as defesas entram aqui).
- **Total (máximo) de um recurso**: o máximo de Vitalidade, Foco, Adrenalina ou Estresse (obrigatório). Vale só enquanto a Condição está ativa.
- **Valor atual de um recurso (dano ou cura por vez)**: o valor corrente de Vitalidade, Foco, Adrenalina ou Estresse (obrigatório). Exige o **Momento**: **ao aplicar** (uma única vez) ou **a cada vez do participante** (toda vez que ele recebe o turno). O ajuste nunca é devolvido. O Momento existe só neste Alvo.
- **Afinidade elemental**: todas as Afinidades ou as de um elemento do catálogo de R0018 (Caminhos ficam de fora, como no campo *Afinidade* da ficha). Um elemento só pode ser removido quando nenhuma Condição o usa.
- **Dano elemental dos ataques**: o modificador do dano elemental que a ficha causa nos ataques, de todos os elementos ou de um elemento do catálogo de R0018 (Caminhos ficam de fora). Não mexe na Vitalidade da ficha.
- **Modificador de dano dos ataques**: o modificador do dano que a ficha causa nos ataques, de todos os tipos ou de um tipo de dano. Não mexe na Vitalidade da ficha: para tirar Vitalidade a cada vez (veneno, sangramento), usa-se o Valor atual de um recurso com valor negativo e o Momento "a cada vez do participante".
- **Maestria**: todas as Maestrias ou as de uma Perícia.

Cada linha tem um **Tipo de valor**, **Fixo** ou **Percentual**, e um **Valor** com sinal, que nunca é 0; o percentual fica entre −100 e 1000. Num Valor atual, um percentual é um percentual do total daquele recurso. Os fixos somam; depois a soma dos percentuais incide sobre o valor já com os fixos, arredondando para baixo.

**Categorias desabilitadas**: a Condição pode, além disso (ou no lugar), desabilitar uma ou mais de oito categorias: **Armas**, **Armaduras**, **Escudos**, **Artefatos**, **Magias**, **Habilidades**, **Runas** e **Passivas**. Quais itens dentro da categoria são desabilitados (toda a categoria ou itens escolhidos) o Mestre decide ao aplicar a Condição ao participante.

**Quando age**: uma Condição só tem efeito enquanto o participante a tem **e** o encontro está **em andamento**; em preparação ou encerrado, nada muda (ver "[[Requisitos - Gerenciador de Encontros]]").

**Editar ou excluir**: a Condição aplicada a um participante é uma **cópia independente** do catálogo (ver a legenda de "[[Requisitos - Modelo de Dados]]"). Editar ou excluir uma entrada do catálogo **não altera** uma Condição já aplicada; só vale para as aplicações seguintes. Excluir é sempre permitido, e o nome da entrada excluída pode ser usado de novo. Uma entrada com o mesmo nome de outra não excluída é recusada com uma mensagem.

# **R0018** - O Auditor mantém o catálogo de elementos e o total de afinidade por Círculo.

**Descrição**: A página **Auditoria → Afinidades** (a mesma de R0016, que continua) passa a manter também o **catálogo de elementos**, usado em todo o app: a lista de Afinidades da ficha de Personagem e de NPC (2.c e o campo *Afinidade* de 2.b), a *Afinidade* da Criatura (1.a), o alvo *Afinidade elemental* e o dano elemental das Condições (R0017) e o requisito *Afinidade Elemental* das Habilidades Passivas ("[[Requisitos - Banco de Magias e Habilidades]]"). Um elemento criado aqui aparece em todos esses lugares; não existe segunda lista.

- **Elemento**: tem **nome**, **tipo** (*Elemento base*, *Caminho* ou *Sub-Elemento*) e **teto** de pontos (R0019). Cada elemento tem um **id estável**, separado do nome: o Auditor pode renomeá-lo sem perder nada que o referencia. Os elementos de hoje entram no catálogo: os 18 que o app já gravava (Terra, Água, Fogo, Ar, Gelo, Flora, Ferro, Raio, Prever, Alma, Purificar, Ecomancia, Hemomancia, Curar, Vida, Aprimorar, Necromancia e Invocação), com os mesmos ids, mais **Mundano**, com o id 18. Ao todo: 4 base (Terra, Água, Fogo, Ar), 3 Caminhos (Alma, Vida, Mundano) e 12 Sub-Elementos.
- **Criar, editar e remover**: o nome é obrigatório e único. **Remover** é recusado enquanto o elemento tem pontos em alguma ficha, ou está escolhido como *Afinidade* de alguma ficha de Personagem, NPC ou Criatura (conta como ficha), ou é usado por alguma Condição ou Passiva, com a mensagem `Este elemento é usado por {n} ficha(s), {c} Condição(ões) e {p} Passiva(s).` (as contagens zero são omitidas).
- **Total por Círculo**: uma tabela com o **máximo de pontos de afinidade** de cada Círculo (a base do máximo de "[[Requisitos - Ficha de Personagem]]" R0007). Os valores iniciais vêm da coluna *Afinidade (Valores Absolutos)* da "[[Tabela de Circulo e Grau por EAP]]" e são semeados **só enquanto a tabela está vazia**; depois vale o que o Auditor mantém e editar o Markdown não a altera mais (mesmo padrão de R0013).

# **R0019** - O Auditor define o teto de pontos de cada elemento.

**Descrição**: Cada elemento do catálogo (R0018) tem um **teto** de pontos por ficha: um inteiro ≥ 1 (0 não faz sentido: o elemento seria inutilizável; para impedir o uso, o Auditor tira a ligação de R0021) ou **sem limite** (valor inicial de todos). O teto define o número de segmentos da barra da ficha; com **sem limite**, a barra tem segmentos até o **máximo da ficha** (R0007). O teto é checado ao subir pontos ("[[Requisitos - Ficha de Personagem]]" R0007). Baixar o teto abaixo do que uma ficha já tem **não** apaga pontos: a ficha fica inconsistente e só aceita tirar pontos (2.c).

# **R0020** - O Auditor mantém os cruzamentos que geram os Sub-Elementos.

**Descrição**: Um **cruzamento** liga **dois elementos** a um **Sub-Elemento** e guarda o **mínimo de pontos exigido em cada um dos dois** (padrão **1** e **1**). Esse mínimo é o requisito do Sub-Elemento na ficha ("[[Requisitos - Ficha de Personagem]]" 2.c). O Auditor edita os cruzamentos existentes e cria novos; o par de elementos é simétrico e único. Os 12 cruzamentos de hoje entram como estão, com requisito 1 e 1: Ar+Água=Gelo, Ar+Fogo=Raio, Água+Terra=Flora, Fogo+Terra=Ferro, Ar+Alma=Prever, Água+Alma=Purificar, Fogo+Vida=Curar, Terra+Vida=Aprimorar, Ar+Mundano=Ecomancia, Água+Mundano=Hemomancia, Fogo+Mundano=Necromancia, Terra+Mundano=Invocação.

# **R0021** - O Auditor define os elementos que cada Vocação e cada Classe vê.

**Descrição**: Para cada **Vocação mágica** (Feiticeiro, Adepto, Bruxo) e para cada **Classe**, o Auditor marca quais **Caminhos e Sub-Elementos** ela vê. Um deles aparece na ficha se estiver ligado à Vocação **ou** à Classe da ficha ("[[Requisitos - Ficha de Personagem]]" 2.c); os 4 Elementos base sempre aparecem. Os valores iniciais ligam Feiticeiro, Adepto e Bruxo a **todos** os Caminhos e Sub-Elementos, para que nenhuma ficha existente fique inconsistente; o Auditor restringe depois. As Vocações marciais (Campeão e Caçador) veem todos os elementos e não têm ligação. A ligação é guardada por um identificador estável da Vocação/Classe, para que Vocações e Classes criadas no futuro só troquem a origem da lista. Mudar uma ligação nunca apaga pontos de ficha (ver 2.c, ficha inconsistente).
