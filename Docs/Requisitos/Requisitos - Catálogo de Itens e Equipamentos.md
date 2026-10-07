> Esta página se baseia no bloco **PAINEL DO *GM*** (ferramenta 4, "Itens e Equipamentos") do "[[01 - Visão Geral.canvas]]".

  

> **Modelo de edição**: apenas o **GM** tem acesso a esta página. Criação, edição e exclusão de itens são exclusivas do GM.

  

> **Exemplo de dados**: um levantamento real de itens do sistema (equipamentos, munições, armas, armaduras e escudos) está em `Docs/Sistema RPG/ruina-itens.docx`. Os requisitos abaixo espelham os esquemas de campos encontrados lá.

  

> **Catálogo inicial**: todo GM começa com esse mesmo levantamento (`ruina-itens.docx`) já cadastrado como itens de catálogo — nenhum tipo Artefato, já que o documento-fonte não descreve nenhum. Peso e Preço nascem em **0** e RF/RM ficam **vazios** em todos eles, pois o documento não traz esses valores (a Durabilidade vem do Rank de cada Arma, ver R0004); um Escudo cujo "Req. Fortitude" a fonte descreve é gravado como o requisito "Vigor ≥ N" (R0014) — não existe um requisito de Fortitude separado no app. A partir daí esses itens são cadastros comuns: o GM edita ou exclui qualquer um deles como faria com um item criado do zero (R0007).

  

> **Visibilidade a jogadores**: o catálogo aqui descrito é a biblioteca central do GM. Tornar um item visível a jogadores é uma decisão tomada por Campanha, não por este catálogo — ver "[[Requisitos - Campanha]]".

  

> **Convenção de campos**: salvo indicação em contrário, todo campo tem como valor padrão o valor salvo no banco de dados; quando o valor for NULL ou não fizer sentido para o item (ex: Rank em um item que não tem Rank), o campo exibe um travessão (**—**).

  

# **R0001** - O catálogo deve listar os itens cadastrados com filtros avançados.

**Descrição**: A página exibe uma lista de todos os itens cadastrados pelo GM, com filtros combináveis por:

- **Nome**: busca por texto livre (substring, sem diferenciar maiúsculas/minúsculas).
- **Tipo**: Item Geral, Arma, Armadura, Escudo ou Artefato (ver R0002).
- **Subcategoria**: depende do Tipo selecionado (ver R0003 e R0004). Como o número de subcategorias cadastradas cresce livremente (dropdown extensível, ver R0003/R0004), o filtro é uma lista pesquisável (o GM digita para filtrar as subcategorias já em uso, em vez de rolar uma lista longa).
- **Rank / Categoria**: Rank (F a SS) para Armas, Armaduras e Escudos; Categoria (Leve/Médio/Pesada) para Armaduras e Escudos.
- **Tipo de Dano**: Cortante, Perfurante, Contundente ou Mágico (específico de Armas).

  

# **R0002** - O GM deve poder criar um item escolhendo um Tipo.

**Descrição**: Ao criar um item, o GM primeiro escolhe um **Tipo**: **Item Geral**, **Arma**, **Armadura**, **Escudo** ou **Artefato**. O formulário de cadastro exibe os campos correspondentes ao tipo escolhido (ver R0003 a R0006 e R0009). O Tipo não pode ser alterado depois de criado — para mudar o tipo de um item, o GM exclui e recria.

  

# **R0003** - Campos de um item do tipo Item Geral.

**Descrição**: Um Item Geral tem os campos:

- **Nome**: text input.
- **Subcategoria**: dropdown extensível — o GM pode escolher uma subcategoria existente ou cadastrar uma nova. As existentes são as já em uso em itens do mesmo Tipo no catálogo do GM; cadastrar uma nova é digitá-la — ela passa a ser sugerida depois que o item é salvo, sem cadastro à parte. Na base de itens fixos da Auditoria de Equipagem o campo se comporta igual, sugerindo as subcategorias já em uso na própria base. As subcategorias observadas no levantamento atual são: Equipamentos de Aventura, Equipamentos Animais, Munição, Alimentação, Serviços, Veículos e Materiais de Estudo & Rituais.
- **Descrição**: texto livre, descrevendo o efeito ou uso do item (ex: "+10 em testes de Arrombamento").
- **Peso**: valor numérico decimal (float) ≥ 0 — itens podem pesar frações, ex. `0,1`. Usado no cálculo de Sobrepeso (ver "[[Requisitos - Ficha de Personagem]]", Sub-Atributos).
- **Capacidade Extra**: opcional, valor numérico decimal ≥ 0. Quando preenchido, o item é tratado como um recipiente (ex: mochila) — ao entrar no Inventário de uma ficha (ver "[[Requisitos - Ficha de Personagem]]" 5.a), seu próprio Peso deixa de contar no Peso Atual do personagem, e `Capacidade Extra × Qtd` passa a somar ao Peso Máximo. Quando NULL ou 0, o item se comporta normalmente (sem esse efeito).
- **Preço**: valor numérico inteiro ≥ 0, em Ciclos (ver R0008).
- **Imagem**: opcional. Upload nos formatos WebP, JPEG, JPG, PNG e GIF, no tamanho máximo definido pela variável *img_max_size* no **.env** — mesma convenção da Imagem do Personagem (ver "[[Requisitos - Ficha de Personagem]]" 1.a).

  

# **R0004** - Campos de um item do tipo Arma.

**Descrição**: Uma Arma tem os campos:

- **Nome**: text input.
- **Subcategoria**: dropdown extensível, como em R0003. As subcategorias observadas são: Lâminas (Facas e Punhais), Espadas, Machados, Lanças, Mangual, Clavas e Bastões, Facas de Caça, Arcos, Varinhas Mágicas e Cajados Mágicos.
- **Rank**: dropdown com os valores F, E, D, C, B, A, S, SS (opcional). Define a Durabilidade da arma (ver abaixo).
- **Empunhadura**: dropdown com os valores "Uma Mão" ou "Duas Mãos".
- **Dados**: text input em notação de dado (ex: `2D10`) — quais e quantos dados a arma rola. Não é um campo numérico porque varia em quantidade e face do dado. Exibe travessão para armas que não rolam dado próprio (ex: Facas de Caça, que só somam um modificador a outra arma).
- **Dano**: valor numérico — o modificador fixo somado ao resultado dos Dados (ex: `+5`).
- **Crítico**: text input. Convive tanto notação de limiar (ex: `19`, o valor mínimo no dado para crítico) quanto de multiplicador (ex: `2x`), conforme observado nos dados reais.
- **Alcance**: valor numérico em Hex (grid tático, ver "[[Ruína RPG - Sistema Básico]]" §4).
- **Tipo de Dano**: dropdown com os valores Cortante, Perfurante, Contundente ou Mágico.
- **Descrição**: texto livre, mesmo comportamento de R0003.
- **Peso**: valor numérico decimal (float) ≥ 0, mesmo comportamento de R0003.
- **Preço**: valor numérico inteiro ≥ 0, em Ciclos (ver R0008).
- **Imagem**: opcional, mesmo comportamento de R0003.
- **Durabilidade**: não é mais digitada — vem do Rank, conforme a [[Tabela de Durabilidade por Rank]] (editável pelo Auditor de Regras); sem Rank, o item não tem durabilidade; Rank inquebrável → a ficha mostra "Inquebrável". Este é o valor **máximo** (ver "[[GRAUS & CÍRCULOS]]", efeito "Ato Múltiplo", que já referencia a durabilidade da arma como recurso consumível); o valor **atual** só existe quando a arma está vinculada a uma ficha (ver "[[Requisitos - Ficha de Personagem]]" 3.a).

  

# **R0005** - Campos de um item do tipo Armadura.

**Descrição**: Uma Armadura tem os campos:

- **Nome**: text input.
- **Categoria**: dropdown com os valores Leve, Médio ou Pesada.
- **Rank**: mesmo dropdown de R0004 (F a SS), opcional.
- **Defesa**: valor numérico concedido por peça equipada.
- **RF (Redução Física)**: valor numérico por peça equipada (ver "Redução Física" em "[[Formulas]]").
- **RM (Redução Mágica)**: valor numérico por peça equipada (ver "Redução Mágica" em "[[Formulas]]").
- **Descrição**: texto livre, mesmo comportamento de R0003.
- **Peso**: valor numérico decimal (float) ≥ 0, mesmo comportamento de R0003 (referente a uma peça).
- **Preço**: valor numérico inteiro ≥ 0, em Ciclos (ver R0008), referente a uma peça.
- **Imagem**: opcional, mesmo comportamento de R0003.
- **Durabilidade**: não é mais digitada — vem do Rank, conforme a [[Tabela de Durabilidade por Rank]] (editável pelo Auditor de Regras); sem Rank, o item não tem durabilidade; Rank inquebrável → a ficha mostra "Inquebrável". Referente a uma peça.

  

# **R0006** - Campos de um item do tipo Escudo.

**Descrição**: Um Escudo tem os campos:

- **Nome**: text input.
- **Categoria**: dropdown com os valores Leve, Médio ou Pesada.
- **Rank**: mesmo dropdown de R0004 (F a SS), opcional.
- **Bônus de Defesa**: valor numérico (ex: `+10`).
- **Descrição**: texto livre, mesmo comportamento de R0003.
- **Peso**: valor numérico decimal (float) ≥ 0, mesmo comportamento de R0003.
- **Preço**: valor numérico inteiro ≥ 0, em Ciclos (ver R0008).
- **Imagem**: opcional, mesmo comportamento de R0003.
- **Durabilidade**: não é mais digitada — vem do Rank, conforme a [[Tabela de Durabilidade por Rank]] (editável pelo Auditor de Regras); sem Rank, o item não tem durabilidade; Rank inquebrável → a ficha mostra "Inquebrável".

  

# **R0007** - O GM deve poder editar e excluir qualquer item do catálogo.

**Descrição**: A partir da lista (R0001), o GM pode abrir um item para editar qualquer um dos seus campos (exceto o Tipo, ver R0002) ou excluí-lo do catálogo.

  

# **R0008** - Todo item deve ter um Preço em Ciclos.

**Descrição**: **Preço**: valor numérico inteiro ≥ 0, na moeda do sistema (**Ciclos**, ver "[[Requisitos - Ficha de Personagem]]", Posses). Presente em todos os 5 tipos de item (R0003 a R0006 e R0009), junto com Peso e Imagem.

  

# **R0009** - Campos de um item do tipo Artefato.

**Descrição**: Um Artefato tem os campos:

- **Nome**: text input.
- **Tipo de alvo**: dropdown fixo — **Atributo**, **Perícia**, **Sub-Atributo** ou **Dano** (ver "[[Requisitos - Ficha de Personagem]]" 5.b).
- **Alvo**: dropdown cujas opções dependem do Tipo de alvo, mesmo comportamento de "[[Requisitos - Ficha de Personagem]]" 5.b.
- **Valor**: campo numérico, o bônus concedido.
- **Descrição**: texto livre, mesmo comportamento de R0003.
- **Peso** e **Preço**: mesmo comportamento de R0003.
- **Imagem**: opcional, mesmo comportamento de R0003.

  

# **R0010** - Uma imagem de item, NPC ou Criatura pode ser referenciada em vez de reenviada.

**Descrição**: Para economizar espaço em disco, qualquer campo de imagem no sistema que aceite upload (ex: entradas de diário, ver "[[Requisitos - Ficha de Personagem]]" 6 e "[[Requisitos - Campanha]]" R0005/R0011) também deve oferecer a opção de **referenciar** uma imagem já cadastrada, em vez de enviar um novo arquivo. Referenciar não duplica o arquivo — aponta para a mesma imagem já salva. As imagens referenciáveis são:

- A **Imagem** de um item do catálogo (R0003 a R0006, R0009).
- A **Imagem do Personagem** de uma Ficha de NPC ou de Criatura (ver "[[Requisitos - Ficha de Personagem]]" 1.a; campo controlado por "[[Requisitos - Ficha de NPCs]]" R0004 e "[[Requisitos - Ficha de Criaturas]]" R0003).

Acesso:

- O **GM** pode referenciar a Imagem de qualquer item, NPC ou Criatura seus.
- Um **jogador** só pode referenciar uma imagem que esteja disponível para ele: de um item anexado a uma campanha da qual é membro e marcado como público (ver "[[Requisitos - Campanha]]" R0006 e R0008); ou de um NPC/Criatura anexado a essa campanha com a Imagem individualmente liberada (ver "[[Requisitos - Ficha de NPCs]]" R0004 e "[[Requisitos - Ficha de Criaturas]]" R0003).

# **R0011** - O escopo de R0010 vale para todo campo de Item na ficha, não só o de reaproveitar Imagem.

> R0010 já limita um jogador a imagens de itens anexados-e-públicos à sua campanha. A mesma regra vale de forma geral para qualquer campo de Item nas fichas (Armas, Armaduras, Escudos, Inventário, Artefatos — Requisitos - Ficha de Personagem R0003): um jogador só pode escolher um Item que esteja anexado à campanha da ficha **e** marcado como público (Requisitos - Campanha R0008/R0009), nunca todo o catálogo do GM.

# **R0012** - Aplicar um kit de Equipagem copia para o catálogo do GM os itens fixos que ele ainda não tem.

**Descrição**: Os itens fixos de um kit de Equipagem vêm da base global de itens fixos (ver "[[Requisitos - Auditoria de Regras]]" R0014 e "[[Requisitos - Modelo de Dados]]", EquipmentKitFixedItems), mas cada GM tem seu próprio catálogo de Itens (R0002). Ao aplicar um kit, cada item fixo que o GM ainda não tem — **de qualquer Tipo**, incluindo Arma, Armadura, Escudo e Artefato — é **copiado completo** para o catálogo dele, com todos os campos da base (inclusive Requisitos e Penalidade), sem imagem. Um item do catálogo do GM com o mesmo **Nome e Tipo** é reaproveitado **sem nenhuma alteração**. A cópia pertence ao GM daí em diante: edições posteriores do Auditor na base **não chegam** a ela, e um item fixo renomeado pelo Auditor gera uma cópia nova na próxima aplicação. O limite da ficha continua valendo (ver "[[Requisitos - Ficha de Personagem]]", Posses, Artefatos: no máximo 3 Artefatos por Tipo de alvo): a aplicação do kit é recusada quando daria à ficha mais Artefatos de um Tipo de alvo do que o limite permite, mesmo quando o kit criaria o Artefato no catálogo — e, nesse caso, nada é criado.

# **R0013** - Armadura, Escudo e Artefato também têm o campo Subcategoria, com um construtor opcional para "Item Inicial".

**Descrição**: O campo **Subcategoria** — já descrito para Item Geral e Arma (R0003/R0004) — passa a existir também nos tipos **Armadura**, **Escudo** e **Artefato** (R0005, R0006 e R0009), com o mesmo comportamento: texto livre, com dropdown extensível sugerindo os valores já em uso.

Além do texto livre, o formulário de cadastro desses quatro tipos (Arma, Armadura, Escudo e Artefato) oferece um checkbox **"Item Inicial"**. Marcado, ele substitui o campo de texto livre por dois selects — **Categoria** e **Família** — cujas opções vêm de um vocabulário global mantido pelo Auditor de Regras na página de Auditoria de Equipagem já existente (ver "[[Requisitos - Modelo de Dados]]", SubcategoriaOptions; não há uma página nova). Ao escolher as duas opções, a Subcategoria salva é o texto composto:

`"Equipamento inicial - {Tipo} - {Categoria} - {Família}"`

(ex: `Equipamento inicial - Armadura - Leve - Couro`). O prefixo `"Equipamento inicial"` é o único marcador de que o valor veio do construtor — não existe uma flag booleana separada gravada no item; o checkbox em si é um estado puramente do formulário (client-side), nunca enviado ao servidor como campo próprio. Por isso, os valores de Categoria e Família cadastrados pelo Auditor de Regras não podem conter o separador `" - "` (cadastro rejeitado).

# **R0014** - Requisitos e Penalidade de equipamentos.

**Descrição**: O formulário de **Arma**, **Armadura**, **Escudo** e **Artefato** termina com o checkbox **"Possui requisitos"**. Marcado, ele abre duas seções:

- **Requisitos**: o que a ficha precisa ter para usar o item sem penalidade. Todos os campos são opcionais; os preenchidos valem todos ao mesmo tempo (a ficha precisa cumprir cada um):
  - **Vocação** e **Classe**: a ficha tem de ter a Vocação ou a Classe escolhida (ver "[[Tabela de Vocação]]" e "[[Tabela de Classes]]");
  - **Atributo**, **Perícia** e **Sub-Atributo**: linhas com o alvo e o valor mínimo exigido do Total na ficha;
  - **Afinidade**: a Afinidade escolhida na ficha (ver "[[Requisitos - Ficha de Personagem]]" 1.a) tem de ser a indicada;
  - **Estrela**: a ficha precisa ter a Estrela escolhida (ver "[[As estrelas alkerianas]]").
- **Penalidade**: o que o item custa à ficha enquanto os Requisitos não são cumpridos. São linhas sobre **Atributo**, **Perícia** ou **Sub-Atributo**, cada uma com um valor ≥ 1 que é **subtraído** do Total do alvo, mais o texto livre **"Outras penalidades"**, que é só exibido e não altera nenhum número.

A lista do catálogo **não** mostra os Requisitos nem a Penalidade dos itens: eles são vistos no formulário do item (Editar) e no popup do equipamento na ficha. "Outras penalidades" é **somente exibido** — no popup do equipamento na ficha aparece marcado como não aplicado automaticamente — e **nunca é aplicado**. Uma Penalidade em um item **sem Requisitos** também nunca é aplicada (não há o que deixar de cumprir); o popup do equipamento na ficha apenas a mostra como informação, assim como mostra a Penalidade de um item cujos Requisitos estão cumpridos. Desmarcar "Possui requisitos" **apaga** os Requisitos e a Penalidade do item.

Os Requisitos **nunca bloqueiam**: a ficha pode adicionar, equipar e usar o item mesmo sem cumpri-los. Enquanto não são cumpridos, a Penalidade é aplicada automaticamente aos Totais da ficha (Atributo, Perícia e Sub-Atributo, e tudo que deriva deles, seguindo as fórmulas de "[[Formulas]]") — ver "[[Requisitos - Ficha de Personagem]]" 3.a, 3.b, 3.c e 5.b. Só conta o item **em uso**: Arma ou Escudo **equipados**, Armadura **num slot** e Artefato **na lista** da ficha. As Penalidades de vários itens se **somam**. Um Artefato que não cumpre seus Requisitos continua dando o próprio bônus; só a Penalidade dele é somada. Os Requisitos são sempre conferidos sobre a ficha **sem** as Penalidades, para que uma penalidade não faça outro requisito deixar de ser cumprido por efeito cascata.

**Migração (versão 1.4.3)**: os campos antigos foram convertidos. O "Requisito de Vigor" de Armadura e Escudo virou o requisito "Vigor ≥ N"; o texto "Penalidade" de Armadura e Escudo foi para "Outras penalidades"; o "Requisito de Atributo" da Arma virou um requisito estruturado quando estava escrito como número + atributo (ex: `10 Dex`) e, nos demais casos, foi para "Outras penalidades" com o prefixo "Requisito: ".
