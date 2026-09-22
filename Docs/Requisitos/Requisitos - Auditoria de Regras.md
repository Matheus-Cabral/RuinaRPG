> Esta página não corresponde a nenhum bloco do "[[01 - Visão Geral.canvas]]" ainda — é um subsistema novo.

  

> **Modelo de acesso**: pertence a um único usuário por vez na prática (embora nada no modelo de dados imponha isso), designado fora do app — um administrador do servidor roda um comando `make` que marca uma conta de GM existente como Auditor de Regras. Nenhum jogador, e nenhum GM que não tenha sido designado assim, alcança as páginas deste documento.

  

# **R0001** - O Auditor de Regras é designado por e-mail, via comando `make` no console do servidor.

**Descrição**: `make grant-rules-auditor EMAIL=...` marca a conta de GM com aquele e-mail (comparação sem diferenciar maiúsculas/minúsculas) como Auditor de Regras; `make revoke-rules-auditor EMAIL=...` desfaz. Só uma conta de **GM** pode ser concedida (uma tentativa contra um e-mail de Jogador, ou um e-mail inexistente, falha com uma mensagem clara no log do servidor); revogar não tem essa restrição. A concessão é verificada diretamente no banco a cada requisição — não fica embutida no token da sessão — então tem efeito imediato, sem o Auditor precisar deslogar e logar de novo.

  

# **R0002** - O Auditor de Regras pode editar o texto do Livro de Regras.

**Descrição**: Uma página lista os 3 documentos do "[[Requisitos - Livro de Regras]]" que não são a aba de Características — Sistema Básico, Graus & Círculos, Tabela de Níveis — cada um com o Markdown atual (a sobrescrita salva, ou o texto padrão do arquivo-fonte) em um campo de texto editável, e um botão "Restaurar padrão" que apaga a sobrescrita. A página exibe um aviso fixo: editar aqui só muda o texto exibido nesta página — nenhuma fórmula ou tabela usada nos cálculos das fichas (Vitalidade, Foco, Graduação, XP por nível, etc.) é afetada, essas continuam fixas no sistema.

  

# **R0003** - O Auditor de Regras tem CRUD completo sobre o catálogo de Características.

**Descrição**: Uma página separada lista todas as Características (Positivas e Negativas, ver "[[Características]]"), cada uma com Nome, Custo, Polaridade, Descrição e a flag "Exige Especificação?" editáveis, mais um botão de exclusão por linha e um formulário para adicionar uma nova. O sinal do Custo deve ser consistente com a Polaridade (Positiva ≥ 0, Negativa ≤ 0) — uma tentativa de salvar um valor inconsistente é rejeitada. Excluir uma característica já usada em alguma Ficha de Personagem/NPC é rejeitado, com uma mensagem indicando o conflito.

Diferente do Catálogo de Itens ou do Banco de Magias, esse catálogo não é por GM — a mudança vale para o servidor inteiro, todo GM e jogador vê o resultado. Editar ou excluir uma característica originalmente vinda do documento-fonte não é desfeito por uma futura atualização/reinicialização do servidor.

  

# **R0004** - A aba "Características" do Livro de Regras reflete esse catálogo em tempo real.

**Descrição**: Ao contrário dos outros 3 documentos (R0002), a aba de Características do "[[Requisitos - Livro de Regras]]" não tem uma sobrescrita de texto própria — ela é montada diretamente a partir do catálogo de R0003. Uma edição salva em R0003 aparece nessa aba imediatamente, sem precisar de nenhuma ação adicional do Auditor.

  

# **R0005** - O Auditor de Regras tem CRUD completo sobre um catálogo separado de características exclusivas de Criatura.

**Descrição**: Uma página separada de R0003 (mesmo formato: Nome, Custo, Polaridade, Descrição, "Exige Especificação?", exclusão por linha, formulário de adicionar, mesma consistência de sinal do Custo, mesmo bloqueio de exclusão em uso) lista um catálogo à parte — **não é a mesma tabela de R0003**. Só a Ficha de Criatura pode conceder uma característica desse catálogo (junto com as de R0003, no mesmo seletor); Personagem, NPC e a aba "Características" do "[[Requisitos - Livro de Regras]]" nunca têm acesso a ele. Assim como R0003, é global (vale para o servidor inteiro, não por GM).

# **R0006** - O Auditor de Regras tem CRUD sobre o catálogo de Efeitos de "[[GRAUS & CÍRCULOS]]".

**Descrição**: Uma página separada lista, agrupados por Grau/Círculo, todos os Efeitos que uma Magia/Habilidade pode comprar — Nome, Tipo de Custo (Fixo, Por Unidade, Manual, Manual por Unidade, ou Derivado de outro Efeito), o valor de custo correspondente, o rótulo da unidade (quando houver Quantidade), o teto de Quantidade (quando houver, podendo escalar com o Grau/Círculo da Magia/Habilidade), e os grupos de pré-requisito (um Efeito pode exigir que um ou mais outros já estejam na mesma Magia/Habilidade, incluindo grupos alternativos "ou"). O catálogo nasce de um seed inicial extraído de "[[GRAUS & CÍRCULOS]]" — quando o próprio texto do sistema deixa um Efeito sem definição (ex.: "Selar", citado como pré-requisito alternativo do Detrito mas nunca definido), o Auditor cadastra a entrada faltante diretamente aqui, sem precisar de uma alteração de código. Assim como o catálogo de Características (R0003), é global (vale para o servidor inteiro, não por GM) e uma edição não é desfeita por uma futura atualização/reinicialização do servidor.

# **R0007** - O Custo em PI de cada Efeito numa Magia/Habilidade é calculado automaticamente a partir do catálogo de R0006.

**Descrição**: Em toda tela que adiciona um Efeito a uma Magia/Habilidade (Banco de Magias e as 3 Fichas), o Nome do Efeito é escolhido de uma lista — não mais um campo de texto livre — restrita aos Efeitos cujo Grau já foi desbloqueado pelo Grau/Círculo da própria Magia/Habilidade (acesso cumulativo: um Efeito de Grau 2 continua disponível numa Magia de Grau 5) e cujos pré-requisitos já estão presentes na mesma lista de Efeitos. O Custo em PI é somente-leitura, calculado a partir do Tipo de Custo do Efeito escolhido — exceto os tipos Manual/Manual por Unidade, onde o próprio livro de regras deixa o valor a critério do Mestre, e que por isso ganham um campo numérico para essa entrada manual. O servidor recusa (400) qualquer submissão cujo Custo em PI não bata com o valor recalculado, cujo Grau não esteja desbloqueado, ou cujos pré-requisitos não estejam satisfeitos — a tela guia, o servidor garante.

  

# **R0008** - O Auditor de Regras tem CRUD completo sobre o catálogo de Históricos.

**Descrição**: Uma página separada lista todos os Históricos (ver "[[Historico]]"), cada um com Nome, Descrição e as duas Perícias bonificadas (+6 e +3) editáveis, mais um botão de exclusão por linha e um formulário para adicionar um novo. As duas Perícias de uma mesma entrada não podem ser iguais — uma tentativa de salvar as duas iguais é rejeitada. Excluir um Histórico já usado em alguma Ficha de Personagem/NPC é rejeitado, com uma mensagem indicando o conflito.

Assim como o catálogo de Características (R0003), esse catálogo não é por GM — a mudança vale para o servidor inteiro. Editar ou excluir um Histórico originalmente vindo do documento-fonte não é desfeito por uma futura atualização/reinicialização do servidor.
