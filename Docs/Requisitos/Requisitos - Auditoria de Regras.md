> Esta página não corresponde a nenhum bloco do "[[01 - Visão Geral.canvas]]" ainda — é um subsistema novo.

  

> **Modelo de acesso**: pertence a um único usuário por vez na prática (embora nada no modelo de dados imponha isso), designado fora do app — um administrador do servidor roda um comando `make` que marca uma conta de GM existente como Auditor de Regras. Nenhum jogador, e nenhum GM que não tenha sido designado assim, alcança as páginas deste documento.

  

# **R0001** - O Auditor de Regras é designado por e-mail, via comando `make` no console do servidor.

**Descrição**: `make grant-rules-auditor EMAIL=...` marca a conta de GM com aquele e-mail (comparação sem diferenciar maiúsculas/minúsculas) como Auditor de Regras; `make revoke-rules-auditor EMAIL=...` desfaz. Só uma conta de **GM** pode ser concedida (uma tentativa contra um e-mail de Jogador, ou um e-mail inexistente, falha com uma mensagem clara no log do servidor); revogar não tem essa restrição. A concessão é verificada diretamente no banco a cada requisição — não fica embutida no token da sessão — então tem efeito imediato, sem o Auditor precisar deslogar e logar de novo.

  

# **R0002** - O Auditor de Regras pode editar o texto do Livro de Regras.

**Descrição**: Uma página lista os 3 documentos do "[[Requisitos - Livro de Regras]]" que não são a aba de Características nem a Tabela de Níveis (esta é montada pela tabela de R0013) — Sistema Básico, Graus & Círculos, As Estrelas Alkerianas — cada um com o Markdown atual (a sobrescrita salva, ou o texto padrão do arquivo-fonte) em um campo de texto editável, e um botão "Restaurar padrão" que apaga a sobrescrita. A página exibe um aviso fixo: editar aqui só muda o texto exibido nesta página — nenhuma fórmula ou tabela usada nos cálculos das fichas (Vitalidade, Foco, Graduação, XP por nível, etc.) é afetada, essas continuam fixas no sistema.

  

# **R0003** - O Auditor de Regras tem CRUD completo sobre o catálogo de Características.

**Descrição**: Uma página separada lista todas as Características (Positivas e Negativas, ver "[[Características]]"), cada uma com Nome, Custo, Polaridade, Descrição e a flag "Exige Especificação?" editáveis, mais um botão de exclusão por linha e um formulário para adicionar uma nova. O sinal do Custo deve ser consistente com a Polaridade (Positiva ≥ 0, Negativa ≤ 0) — uma tentativa de salvar um valor inconsistente é rejeitada. Excluir uma característica já usada em alguma Ficha de Personagem/NPC é rejeitado, com uma mensagem indicando o conflito.

Diferente do Catálogo de Itens ou do Banco de Magias, esse catálogo não é por GM — a mudança vale para o servidor inteiro, todo GM e jogador vê o resultado. Editar ou excluir uma característica originalmente vinda do documento-fonte não é desfeito por uma futura atualização/reinicialização do servidor.

  

# **R0004** - A aba "Características" do Livro de Regras reflete esse catálogo em tempo real.

**Descrição**: Ao contrário dos outros 3 documentos (R0002), a aba de Características do "[[Requisitos - Livro de Regras]]" não tem uma sobrescrita de texto própria — ela é montada diretamente a partir do catálogo de R0003. Uma edição salva em R0003 aparece nessa aba imediatamente, sem precisar de nenhuma ação adicional do Auditor.

  

# **R0005** - O Auditor de Regras tem CRUD completo sobre um catálogo separado de características exclusivas de Criatura.

**Descrição**: Uma página separada de R0003 (mesmo formato: Nome, Custo, Polaridade, Descrição, "Exige Especificação?", exclusão por linha, formulário de adicionar, mesma consistência de sinal do Custo, mesmo bloqueio de exclusão em uso) lista um catálogo à parte — **não é a mesma tabela de R0003**. Só a Ficha de Criatura pode conceder uma característica desse catálogo (junto com as de R0003, no mesmo seletor); Personagem, NPC e a aba "Características" do "[[Requisitos - Livro de Regras]]" nunca têm acesso a ele. Assim como R0003, é global (vale para o servidor inteiro, não por GM).

# **R0006** - O Auditor de Regras tem CRUD sobre o catálogo de Efeitos de "[[GRAUS & CÍRCULOS]]".

**Descrição**: Uma página separada lista, agrupados por Grau/Círculo (de 1 a 9 — um Grau fora dessa faixa é recusado), todos os Efeitos que uma Magia/Habilidade pode comprar — Nome, Tipo de Custo (Fixo, Por Unidade, Manual, Manual por Unidade, ou Derivado de outro Efeito), o valor de custo correspondente, o rótulo da unidade (quando houver Quantidade), o teto de Quantidade (quando houver, podendo escalar com o Grau/Círculo da Magia/Habilidade), e os grupos de pré-requisito (um Efeito pode exigir que um ou mais outros já estejam na mesma Magia/Habilidade, incluindo grupos alternativos "ou"). O catálogo nasce de um seed inicial extraído de "[[GRAUS & CÍRCULOS]]" — quando o próprio texto do sistema deixa um Efeito sem definição (ex.: "Selar", citado como pré-requisito alternativo do Detrito mas nunca definido), o Auditor cadastra a entrada faltante diretamente aqui, sem precisar de uma alteração de código. Assim como o catálogo de Características (R0003), é global (vale para o servidor inteiro, não por GM) e uma edição não é desfeita por uma futura atualização/reinicialização do servidor.

Criar, editar ou excluir um Efeito aqui também atualiza o bloco desse Efeito (`## Nome`) no documento "[[GRAUS & CÍRCULOS]]" do Livro de Regras (R0002), na mesma gravação: criar insere o bloco ao fim da seção do seu Grau/Círculo (se ainda não houver um bloco com esse nome); editar reescreve o bloco — movendo-o de seção se o Grau/Círculo mudou e renomeando o título se o Nome mudou; excluir remove o bloco. O bloco de um Efeito é o `## Título` cujo texto é igual ao Nome, sem diferenciar maiúsculas/minúsculas nem acentos (por isso o catálogo também recusa um Nome que só difira de outro nisso); as únicas exceções são os blocos compartilhados — "Efeitos Básicos" (exatamente Dano, Alcance e Duração) e "Libra" (as variantes `Libra (…)`) —, que nunca são reescritos nem removidos por essa sincronização. O Livro sempre contém todo Efeito do catálogo: a cada atualização do servidor (make migrate) e ao "Restaurar padrão" desse documento, todo Efeito ausente do texto é acrescentado (sem reescrever nem remover blocos existentes). Como essa verificação só acrescenta, "Restaurar padrão" também traz de volta os blocos originais de Efeitos que tenham sido renomeados ou excluídos no catálogo.

# **R0007** - O Custo em PI de cada Efeito numa Magia/Habilidade é calculado automaticamente a partir do catálogo de R0006.

**Descrição**: Em toda tela que adiciona um Efeito a uma Magia/Habilidade (Banco de Magias e as 3 Fichas), o Nome do Efeito é escolhido de uma lista — não mais um campo de texto livre — restrita aos Efeitos cujo Grau já foi desbloqueado pelo Grau/Círculo da própria Magia/Habilidade (acesso cumulativo: um Efeito de Grau 2 continua disponível numa Magia de Grau 5) e cujos pré-requisitos já estão presentes na mesma lista de Efeitos. O Custo em PI é somente-leitura, calculado a partir do Tipo de Custo do Efeito escolhido — exceto os tipos Manual/Manual por Unidade, onde o próprio livro de regras deixa o valor a critério do Mestre, e que por isso ganham um campo numérico para essa entrada manual. O servidor recusa (400) qualquer submissão cujo Custo em PI não bata com o valor recalculado, cujo Grau não esteja desbloqueado, ou cujos pré-requisitos não estejam satisfeitos — a tela guia, o servidor garante.

  

# **R0008** - O Auditor de Regras tem CRUD completo sobre o catálogo de Históricos.

**Descrição**: Uma página separada lista todos os Históricos (ver "[[Historico]]"), cada um com Nome, Descrição e as duas Perícias bonificadas (+6 e +3) editáveis, mais um botão de exclusão por linha e um formulário para adicionar um novo. As duas Perícias de uma mesma entrada não podem ser iguais — uma tentativa de salvar as duas iguais é rejeitada. Excluir um Histórico já usado em alguma Ficha de Personagem/NPC é rejeitado, com uma mensagem indicando o conflito.

Assim como o catálogo de Características (R0003), esse catálogo não é por GM — a mudança vale para o servidor inteiro. Editar ou excluir um Histórico originalmente vindo do documento-fonte não é desfeito por uma futura atualização/reinicialização do servidor. Renomear um Histórico já seedado não impede que o nome original volte a ser criado como uma nova entrada na próxima reinicialização do servidor (o casamento do seed usa o Nome como chave) — o Auditor deve excluir manualmente a entrada duplicada caso isso aconteça.

# **R0009** - O Auditor de Regras tem CRUD completo sobre o catálogo de kits de Equipagem inicial.

**Descrição**: Uma página separada lista todos os kits de Equipagem (ver "[[Requisitos - Ficha de Personagem]]"), cada um com Nome, Descrição e Ciclos editáveis, mais um formulário para adicionar um novo e um botão de exclusão por kit. Dentro de cada kit, duas sub-tabelas com CRUD próprio: os itens fixos (Nome, Tipo, Quantidade) que todo personagem que escolhe o kit recebe, e os slots de escolha (Label, Tipo, Subcategorias permitidas, Rank, Quantidade) que o jogador preenche ao escolher o kit — cada uma com seu próprio formulário de adicionar e exclusão por linha. Excluir um kit já escolhido em alguma Ficha de Personagem/NPC é rejeitado, com uma mensagem indicando o conflito.

Assim como os catálogos de Características (R0003) e Históricos (R0008), esse catálogo não é por GM — a mudança vale para o servidor inteiro. Renomear um kit já seedado não impede que o nome original volte a ser criado como uma nova entrada na próxima reinicialização do servidor (o casamento do seed usa o Nome como chave, mesma limitação do `HistoricoSeeder` de R0008) — o Auditor deve excluir manualmente a entrada duplicada caso isso aconteça.

# **R0010** - A mesma página gerencia o vocabulário de Categoria/Família usado pelo Construtor de Subcategoria, e os slots de escolha aceitam Armadura/Escudo/Artefato além de Arma.

**Descrição**: Uma seção "Construtor de Subcategoria", na mesma página de R0009, lista — por Tipo (Arma, Armadura, Escudo ou Artefato, escolhido num seletor) — duas listas independentes de valores: Categorias e Famílias. Cada lista tem seu próprio formulário de adicionar e exclusão por linha; não há edição — renomear um valor é excluir e adicionar de novo. Um valor é aparado de espaços nas pontas e não pode ficar vazio, começar ou terminar com `-`, nem conter `,` (usada como separador da lista de Famílias permitidas de um slot de escolha, ver "[[Requisitos - Modelo de Dados]]" EquipmentKitChoiceSlots) ou a sequência `" - "` (usada como separador na string composta de "[[Requisitos - Catálogo de Itens e Equipamentos]]" R0013); a exclusão é lógica (soft delete) — um item do catálogo já montado com aquele valor mantém sua string composta intacta. Esse vocabulário é global (vale para o servidor inteiro, não por GM), como os demais catálogos deste documento.

Os slots de escolha de um kit de Equipagem (a sub-tabela de R0009) passam a aceitar Tipo Arma, Armadura, Escudo ou Artefato — antes só Arma. Um slot de Tipo Armadura exige também um Slot de Armadura (Capacete, Superior ou Inferior), indicando qual posição da ficha aquela escolha preenche; os demais Tipos não têm esse campo. As "Subcategorias" permitidas do slot passam a ser escolhidas entre os valores de Família cadastrados aqui para o Tipo do slot (um multi-seletor, não mais texto livre) — vazio continua significando "qualquer". Itens fixos (a outra sub-tabela de R0009) continuam sem suportar Armadura.

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

- **Acumulativa**: o valor da ficha é a soma dos níveis 1 até o nível atual (célula vazia vale 0). Ex.: Pontos de Atributo, Pontos de Perícia, Espaços de Característica, Pontos de Ignição, Espaços e Pontos de Maestria. Também são Acumulativas as três colunas **Passivas Livres**, **Passivas Vocacionais** e **Passivas De Classe**: o Auditor escreve `1` apenas nos níveis que concedem uma Passiva daquela categoria, e o limite da ficha é a soma da coluna do nível 1 até o nível dela (abaixo do primeiro nível que concede, o limite é 0). Uma coluna de Passivas **inteiramente vazia** significa sem limite para a categoria. Como são Acumulativas, o aviso de subida de nível mostra "Passivas Livres: +1" nos níveis que concedem.
- **Por nível**: vale o número do nível atual; uma célula vazia repete o valor do nível anterior mais próximo, e sem nenhum valor não há limite. Ex.: Máx. de Atributo, Máx. de Perícia. **XP para o próximo nível** e **EAP base** também são Por nível, mas nunca repetem valor: XP vazio no último nível significa nível máximo. O XP do último nível é sempre ignorado, mesmo preenchido: o último nível é o máximo, não existe um nível seguinte (para liberar um novo nível, adicione-o à tabela e preencha o XP do nível anterior).

As colunas acima são do sistema — podem ser renomeadas, não removidas, e alimentam as fichas. O Auditor pode criar colunas próprias (nome + tipo, o tipo não muda depois), que aparecem na ficha só como informação, e removê-las (a remoção pede confirmação e apaga os valores da coluna). Pode também adicionar um nível ao fim da tabela e remover o último nível, desde que nenhuma ficha esteja nele.

**Limites**: Máx. de Atributo e Máx. de Perícia limitam os pontos gastos (Gasto) num atributo ou numa perícia; as colunas de Passivas (Livres, Vocacionais, De Classe), pela soma aditiva descrita acima, limitam quantas Passivas de cada categoria a ficha pode ter. Valem para Personagem e NPC: o servidor recusa salvar acima do limite, mas uma ficha que já está acima continua válida e pode baixar o valor. A Criatura não tem limites, mas o Nível de NPC e Criatura é validado entre 1 e o último nível da tabela. Níveis acima do último das tabelas de Vocação, Classe e Arquétipo (que continuam fixas, com 50 níveis) usam os valores de Vida e Arcana do último nível dessas tabelas. O aviso de subida de nível lista os bônus numéricos como "<Nome da coluna>: +N", seguidos das linhas de Outros bônus.

O Livro de Regras monta a aba Tabela de Níveis a partir desta tabela; ela aparece como uma tabela "Nível | Bônus" (o que cada nível concede, um item por linha: colunas Acumulativas e Outros bônus; "—" quando nada) e, abaixo, "Limites e progressão" com as colunas Por nível que têm algum valor (ver "[[Requisitos - Livro de Regras]]"); ela não é mais editada em Auditoria → Livro de Regras. O Compêndio continua indexando os arquivos Markdown. A tabela é preenchida uma única vez, a partir de "[[Tabela de Níveis]]" e "[[Tabelas de XP, Atributos, Características e EAP]]", quando está vazia; depois disso, editar esses arquivos não altera orçamentos nem limites.
