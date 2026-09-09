> Esta página se baseia no bloco **PAINEL DO *GM*** (ferramenta 4, "Itens e Equipamentos") do "[[01 - Visão Geral.canvas]]".

  

> **Modelo de edição**: apenas o **GM** tem acesso a esta página. Criação, edição e exclusão de itens são exclusivas do GM.

  

> **Exemplo de dados**: um levantamento real de itens do sistema (equipamentos, munições, armas, armaduras e escudos) está em `Docs/Sistema RPG/ruina-itens.docx`. Os requisitos abaixo espelham os esquemas de campos encontrados lá.

  

> **Catálogo inicial**: todo GM começa com esse mesmo levantamento (`ruina-itens.docx`) já cadastrado como itens de catálogo — nenhum tipo Artefato, já que o documento-fonte não descreve nenhum. Peso e Preço nascem em **0** e Durabilidade/RF/RM ficam **vazios** em todos eles, pois o documento não traz esses valores; um Escudo cujo "Req. Fortitude" a fonte descreve é gravado no campo Requisito de Vigor (R0006) — não existe um requisito de Fortitude separado no app. A partir daí esses itens são cadastros comuns: o GM edita ou exclui qualquer um deles como faria com um item criado do zero (R0007).

  

> **Visibilidade a jogadores**: o catálogo aqui descrito é a biblioteca central do GM. Tornar um item visível a jogadores é uma decisão tomada por Campanha, não por este catálogo — ver "[[Requisitos - Campanha]]".

  

> **Convenção de campos**: salvo indicação em contrário, todo campo tem como valor padrão o valor salvo no banco de dados; quando o valor for NULL ou não fizer sentido para o item (ex: Requisito de Atributo em uma arma que não exige nenhum), o campo exibe um travessão (**—**).

  

# **R0001** - O catálogo deve listar os itens cadastrados com filtros avançados.

**Descrição**: A página exibe uma lista de todos os itens cadastrados pelo GM, com filtros combináveis por:

- **Tipo**: Item Geral, Arma, Armadura, Escudo ou Artefato (ver R0002).
- **Subcategoria**: depende do Tipo selecionado (ver R0003 e R0004).
- **Tier / Categoria**: Tier (F a S) para Armas; Categoria (Leve/Médio/Pesada) para Armaduras e Escudos.
- **Tipo de Dano**: Cortante, Perfurante, Contundente ou Mágico (específico de Armas).

  

# **R0002** - O GM deve poder criar um item escolhendo um Tipo.

**Descrição**: Ao criar um item, o GM primeiro escolhe um **Tipo**: **Item Geral**, **Arma**, **Armadura**, **Escudo** ou **Artefato**. O formulário de cadastro exibe os campos correspondentes ao tipo escolhido (ver R0003 a R0006 e R0009). O Tipo não pode ser alterado depois de criado — para mudar o tipo de um item, o GM exclui e recria.

  

# **R0003** - Campos de um item do tipo Item Geral.

**Descrição**: Um Item Geral tem os campos:

- **Nome**: text input.
- **Subcategoria**: dropdown extensível — o GM pode escolher uma subcategoria existente ou cadastrar uma nova. As subcategorias observadas no levantamento atual são: Equipamentos de Aventura, Equipamentos Animais, Munição, Alimentação, Serviços, Veículos e Materiais de Estudo & Rituais.
- **Descrição**: texto livre, descrevendo o efeito ou uso do item (ex: "+10 em testes de Arrombamento").
- **Peso**: valor numérico decimal (float) ≥ 0 — itens podem pesar frações, ex. `0,1`. Usado no cálculo de Sobrepeso (ver "[[Requisitos - Ficha de Personagem]]", Sub-Atributos).
- **Capacidade Extra**: opcional, valor numérico decimal ≥ 0. Quando preenchido, o item é tratado como um recipiente (ex: mochila) — ao entrar no Inventário de uma ficha (ver "[[Requisitos - Ficha de Personagem]]" 5.a), seu próprio Peso deixa de contar no Peso Atual do personagem, e `Capacidade Extra × Qtd` passa a somar ao Peso Máximo. Quando NULL ou 0, o item se comporta normalmente (sem esse efeito).
- **Preço**: valor numérico inteiro ≥ 0, em Ciclos (ver R0008).
- **Imagem**: opcional. Upload nos formatos WebP, JPEG, JPG, PNG e GIF, no tamanho máximo definido pela variável *img_max_size* no **.env** — mesma convenção da Imagem do Personagem (ver "[[Requisitos - Ficha de Personagem]]" 1.a).

  

# **R0004** - Campos de um item do tipo Arma.

**Descrição**: Uma Arma tem os campos:

- **Nome**: text input.
- **Subcategoria**: dropdown extensível, como em R0003. As subcategorias observadas são: Lâminas (Facas e Punhais), Espadas, Machados, Lanças, Mangual, Clavas e Bastões, Facas de Caça, Arcos, Varinhas Mágicas e Cajados Mágicos.
- **Tier**: dropdown com os valores F, E, D, C, B, A, S.
- **Empunhadura**: dropdown com os valores "Uma Mão" ou "Duas Mãos".
- **Dados**: text input em notação de dado (ex: `2D10`) — quais e quantos dados a arma rola. Não é um campo numérico porque varia em quantidade e face do dado. Exibe travessão para armas que não rolam dado próprio (ex: Facas de Caça, que só somam um modificador a outra arma).
- **Dano**: valor numérico — o modificador fixo somado ao resultado dos Dados (ex: `+5`).
- **Crítico**: text input. Convive tanto notação de limiar (ex: `19`, o valor mínimo no dado para crítico) quanto de multiplicador (ex: `2x`), conforme observado nos dados reais.
- **Alcance**: valor numérico em Hex (grid tático, ver "[[Ruína RPG - Sistema Básico]]" §4).
- **Tipo de Dano**: dropdown com os valores Cortante, Perfurante, Contundente ou Mágico.
- **Requisito de Atributo**: campo opcional (ex: `10 Dex`), presente em armas como arcos. Exibe travessão quando a arma não exige atributo mínimo.
- **Descrição**: texto livre, mesmo comportamento de R0003.
- **Peso**: valor numérico decimal (float) ≥ 0, mesmo comportamento de R0003.
- **Preço**: valor numérico inteiro ≥ 0, em Ciclos (ver R0008).
- **Imagem**: opcional, mesmo comportamento de R0003.
- **Durabilidade**: valor numérico inteiro ≥ 0 — o máximo de durabilidade da arma, definido livremente pelo GM (ver "[[GRAUS & CÍRCULOS]]", efeito "Ato Múltiplo", que já referencia a durabilidade da arma como recurso consumível). Este é o valor **máximo**; o valor **atual** só existe quando a arma está vinculada a uma ficha (ver "[[Requisitos - Ficha de Personagem]]" 3.a).

  

# **R0005** - Campos de um item do tipo Armadura.

**Descrição**: Uma Armadura tem os campos:

- **Nome**: text input.
- **Categoria**: dropdown com os valores Leve, Médio ou Pesada.
- **Defesa**: valor numérico concedido por peça equipada.
- **RF (Redução Física)**: valor numérico por peça equipada (ver "Redução Física" em "[[Formulas]]").
- **RM (Redução Mágica)**: valor numérico por peça equipada (ver "Redução Mágica" em "[[Formulas]]").
- **Penalidade**: text input (ex: `-3 Reflexo`). Exibe travessão quando a armadura não tem penalidade.
- **Requisito de Vigor**: valor numérico mínimo de Vigor exigido para equipar sem penalidade adicional. Exibe travessão quando não houver requisito.
- **Descrição**: texto livre, mesmo comportamento de R0003.
- **Peso**: valor numérico decimal (float) ≥ 0, mesmo comportamento de R0003 (referente a uma peça).
- **Preço**: valor numérico inteiro ≥ 0, em Ciclos (ver R0008), referente a uma peça.
- **Imagem**: opcional, mesmo comportamento de R0003.
- **Durabilidade**: valor numérico inteiro ≥ 0, máximo definido pelo GM, mesmo comportamento de R0004 (referente a uma peça).

  

# **R0006** - Campos de um item do tipo Escudo.

**Descrição**: Um Escudo tem os campos:

- **Nome**: text input.
- **Categoria**: dropdown com os valores Leve, Médio ou Pesada.
- **Bônus de Defesa**: valor numérico (ex: `+10`).
- **Penalidade**: text input, mesmo comportamento de R0005.
- **Requisito de Vigor**: valor numérico mínimo exigido para equipar sem penalidade adicional. Exibe travessão quando não houver requisito.
- **Descrição**: texto livre, mesmo comportamento de R0003.
- **Peso**: valor numérico decimal (float) ≥ 0, mesmo comportamento de R0003.
- **Preço**: valor numérico inteiro ≥ 0, em Ciclos (ver R0008).
- **Imagem**: opcional, mesmo comportamento de R0003.
- **Durabilidade**: valor numérico inteiro ≥ 0, máximo definido pelo GM, mesmo comportamento de R0004.

  

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
