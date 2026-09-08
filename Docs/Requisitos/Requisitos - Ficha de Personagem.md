> Esta ficha de personagem se baseia na [[Ruína RPG - Ficha de personagem.pdf]], que foi feita pelo Google planilhas.

  

> **Modelo de edição**: salvo indicação em contrário, todos os campos da ficha são editáveis pelo jogador dono da ficha. Apenas a **criação** e a **exclusão** de uma ficha são exclusivas do **GM** (ver [[01 - Visão Geral.canvas]]).

  

> **Convenção de campos**: salvo indicação em contrário, todo campo tem como valor padrão o valor salvo no banco de dados; quando o valor for NULL, o campo exibe seu *placeholder* em itálico.

  

# **R0001** - A ficha deve ser subdividida em abas.

**Descrição**: A ficha de personagem se subdivide em vários grupos, que tratam de vários subsistemas e guardam informações importantes de vários recursos que um personagem tem. As abas são:

  

1. **Informações Básicas**: identidade do personagem, nível/progressão e recursos. Composta pelos subgrupos abaixo.

  

### 1.a) Identidade

  

- *Imagem do Personagem*: uma imagem de perfil, em que o jogador tem a possibilidade de fazer o upload de uma imagem nos formatos WebP, JPEG, JPG, PNG e GIF no tamanho máximo em **MB** definido pela variável *img_max_size* no **.env**, o usuário pode alterar a imagem do personagem ao clicar em um ícone de lápis, simbolizando edição, que é exibido quando o usuário clicar na imagem para a expandir.

- *Nome do personagem*: Text input que tem como valor padrão o nome do personagem salvo no banco de dados e caso o valor for NULL, exibe o placeholder *Nomeie seu Personagem*.

- *Linhagem*: Um dropdown exibindo o nome das 4 linhagens definidas no §7º do "[[Ruína RPG - Sistema Básico]]", sendo elas, **Humano**, **Phylauc**, **Nephrytes** e **Ecônos**. O valor padrão do dropdown deve ser o salvo no banco de dados e caso o valor for NULL, exibe o placeholder *Escolha uma Linhagem*.

- *Variante*: Um dropdown exibindo os nomes das variantes correspondentes a Linhagem escolhida, sendo as variantes, de Humanos: Sinir e Laonir, de Phylauc: Phylac'tai e Es'Phylauc, de Nephrytes: Yavos e Koroanos, de Ecônos: Alóra. Caso for NULL exibe o placeholder *Escolha uma Variante* e se a linhagem estiver NULL, não exibe nada e mostra uma mensagem alertando o usuário para escolher uma linhagem antes da variante.

- *Vocação*: Um dropdown exibindo as 5 vocações (classes) definidas na "[[Tabela de Vocação]]", sendo elas, **Campeão**, **Caçador**, **Feiticeiro**, **Adepto** e **Bruxo**. O valor padrão do dropdown deve ser o salvo no banco de dados e caso o valor for NULL, exibe o placeholder *Escolha uma Vocação*. A vocação escolhida determina as colunas de Vida e Arcana usadas na progressão de nível (ver *Vida* e *Arcana* no subgrupo Recursos).

- *Sub-vocação*: Um dropdown exibindo os nomes das sub-vocações (subclasses) correspondentes à Vocação escolhida, listadas na "[[Tabela de Classes]]". O comportamento é análogo ao de *Variante* em relação a *Linhagem*: caso for NULL exibe o placeholder *Escolha uma Sub-vocação* e, se a Vocação estiver NULL, não exibe nada e mostra uma mensagem alertando o usuário para escolher uma vocação antes da sub-vocação. A relação de Vocação e Classe está em [[Relação de Vocacão e Classes]].

- *Trabalho*: campo previsto porém ainda não implementado. Vamos apenas não implementa-lo.

- *Afinidade*: Um dropdown com a afinidade elemental do personagem. Os valores correspondem aos 4 Elementos e aos 14 Sub-Elementos definidos na Matriz Elemental (ver 2.c, Afinidades): **Terra**, **Água**, **Fogo**, **Ar**, **Gelo**, **Flora**, **Ferro**, **Raio**, **Prever**, **Alma**, **Purificar**, **Ecomancia**, **Hemomancia**, **Curar**, **Vida**, **Aprimorar**, **Necromancia** e **Invocação**.

- *Propriedade*: Um text input. Deve seguir a convenção de campos (valor do banco; placeholder em NULL).

  

### 1.b) Nível e Progressão

  

- *Nível*: campo **calculado**, de 1 a 50 — não é mais editável diretamente na Ficha de Personagem (NPCs e Criaturas continuam permitindo edição direta pelo GM; ver seus documentos de diff). O valor é derivado da *Experiência atual* segundo os limiares de XP em "[[Tabelas de XP, Atributos, Características e EAP]]"; com 0 de XP, assume **1**. O nível é a chave para os valores de Vida/Arcana da "[[Tabela de Vocação]]" e para os bônus por nível.

- *Graduação*: campo numérico de 0 a 9, calculado a partir do EAP atual segundo a tabela em "[[Tabela de Circulo e Grau por EAP]]". Se a Vocação for **Campeão** ou **Caçador**, o campo é rotulado **Grau**, não editável, e sempre calculado normalmente pelo EAP. Se a Vocação for qualquer outra, o campo é rotulado **Círculo** e depende de um checkbox "Possui coração de mana?" (ver §1 de "[[GRAUS & CÍRCULOS]]"): marcado, o Círculo é calculado normalmente pelo EAP; desmarcado, o Círculo é exibido como **0** independentemente do EAP acumulado (o personagem pode perder um coração de mana em jogo, desmarcando o checkbox). Quando NULL, o checkbox assume **desmarcado** e o valor assume **0**.

- *Experiência atual*: campo numérico (inteiro ≥ 0) que registra a XP acumulada do personagem. Quando NULL assume **0**. Além da edição direta do valor total, um campo dedicado permite adicionar ou subtrair uma quantidade de XP ao total atual (nunca abaixo de **0**); qualquer uma das duas formas recalcula o *Nível* imediatamente.

- *Para o próximo*: valor que indica a XP necessária para o próximo nível. A tabela com os valores está no documento [[Tabelas de XP, Atributos, Características e EAP]].

- *EAP atual*: campo numérico. Segue a tabela no documento [[Tabelas de XP, Atributos, Características e EAP]] e é somado pelo resultado de Âmbares Absorvidos.

- *Pontos de Ignição (PI)*: recurso usado na compra de efeitos de magias e habilidades (ver "[[GRAUS & CÍRCULOS]]"). Exibir o valor **atual**, editável diretamente, e o **total** acumulado. O *total* é **calculado**, não editável diretamente: soma dos bônus de PI por Nível (conforme a "[[Tabela de Níveis]]") mais um bônus manual que o GM concede à parte — um campo dedicado permite adicionar ou subtrair esse bônus manual (mesmo padrão do campo de XP em "Experiência atual", acima). Quando NULL, o *atual* e o bônus manual assumem **0**.

- *Âmbares Absorvidos*: contador numérico dos Âmbares absorvidos pelo personagem. Acompanhado dos contadores por **Rank** (**F**, **E**, **D**, **C**, **B**, **A**, **S**), cada um um inteiro ≥ 0 que assume **0** quando NULL. Cada Âmbar absorvido aumenta o EAP com um valor de acordo com seu Rank, sendo estes valores: Rank F = 5; Rank E = 15; Rank D = 40; Rank C = 120; Rank B = 350; Rank A = 1000; Rank S = 3000.

  

### 1.c) Recursos

  

Cada recurso abaixo é exibido como um par **atual / máximo** (campos numéricos inteiros ≥ 0). O valor atual é editável pelo jogador; o máximo, quando derivado de outra regra, é indicado abaixo. Quando NULL, atual e máximo assumem **0** (exceto onde indicado). As Formulas estão em "[[Formulas]]" e os status de classe estão nas tabelas em "[[Tabela de Vocação]]" e em "[[Tabela de Classes]]".

  

- *Adrenalina (PA)*: máximo = 10 + Artefato (ver "[[Formulas]]" e §3 de "[[Ruína RPG - Sistema Básico]]"). Determina a escala do dado da cena. Atual não pode exceder o máximo; quando NULL, máximo assume **10** (sem bônus de Artefato).

- *Foco (PF)*: recurso gasto em habilidades e magias. Máximo = (Astúcia × 2) + Status de classe Foco, onde Status de classe Foco é a coluna **Arcana** da vocação na "[[Tabela de Vocação]]" e "[[Tabela de Classes]]" (cruzando Vocação × Nível) — ver "[[Formulas]]". Atual não pode exceder o máximo.

- *Estresse*: par atual / máximo; o Máximo é **10**.

- *Vitalidade*: par atual / máximo. Máximo = (Vigor × 2) + Status de classe Vida, onde Status de classe Vida é a coluna **Vida** da vocação na "[[Tabela de Vocação]]" e "[[Tabela de Classes]]" (cruzando Vocação × Nível) — ver "[[Formulas]]". Atual não pode exceder o máximo.

  

2. **Atributos & Perícias**: atributos, sub-atributos, afinidades e perícias. Composta pelos subgrupos abaixo.

  

### 2.a) Atributos

  

Os 8 atributos (ver §1 de "[[Ruína RPG - Sistema Básico]]" para a definição de cada um) são exibidos em bloco nesta ordem — Força, Vigor, Agilidade, Destreza, Astúcia, Instinto, Influência e Vontade —, cada um com os campos:

- *Gasto*: campo numérico ≥ 0, editável pelo jogador. Acumula os pontos alocados manualmente no atributo: os 9 pontos de distribuição inicial da criação de personagem mais os "Pontos de Atributo" recebidos ao subir de nível (ver "[[Tabela de Níveis]]"). Quando NULL, assume **0**. A soma de Gasto de todos os 8 atributos deve ser exibida e não pode ultrapassar o total de pontos que o personagem já recebeu (criação + níveis).
- *Bônus*: campo numérico ≥ 0, editável pelo jogador. Acumula bônus recebidos de outras fontes que não a alocação manual: o bônus racial de Linhagem/Variante (ver §7 de "[[Ruína RPG - Sistema Básico]]") e pontos de atributo comprados com Pontos de Ignição (ver "[[GRAUS & CÍRCULOS]]", efeito "Pontos de Atributo"). Quando NULL, assume **0**.
- *Maestria*: checkbox. Marcado, indica que o jogador investiu um Ponto de Maestria (recurso concedido pela "[[Tabela de Níveis]]") naquele atributo — poço separado do de Maestrias de Perícia (ver 4.e); aqui o checkbox só precisa registrar o estado marcado/desmarcado por atributo e alimentar o cálculo de *Total* abaixo.
- *Total*: campo calculado, não editável. Sem Maestria marcada: `Total = Gasto + (Bônus / 2) + Artefatos`. Com Maestria marcada: `Total = Gasto + Bônus + Artefatos` (ver "[[Formulas]]"). *Artefatos* refere-se à soma dos Valores de Artefatos equipados (ver 5.b) cujo Tipo é Atributo e cujo Alvo é este atributo.

  

### 2.b) Sub-Atributos

  

Sub-atributos são valores derivados, calculados automaticamente (não editáveis diretamente pelo jogador, salvo indicação em contrário). As fórmulas estão em "[[Formulas]]"; "*Bruto [Perícia]*" se refere ao modificador daquela Perícia sozinho, sem somar o atributo-chave dela (ver §2 de "[[Ruína RPG - Sistema Básico]]" — a cada 3 pontos investidos na Perícia, +1 permanente). Perícias são detalhadas em 2.d.

- *Iniciativa*: `Agilidade + Bruto Prontidão + Artefato ou item`.
- *Movimentação*: `(Agilidade × 2) + Artefato − Sobrepeso`, com mínimo absoluto de **1**. *Sobrepeso* = `max(0, Peso Total Carregado − Limite de Carga)`, onde *Limite de Carga* = `piso((Força + Vigor) / 2)` e *Peso Total Carregado* é a soma do campo Peso (ver "[[Requisitos - Catálogo de Itens e Equipamentos]]") de todos os itens no Inventário e no arsenal de Armas/Escudos do personagem, equipados ou não (ver aba "Posses" e 3.a/3.c).
- *Esquiva Natural*: `Agilidade + Bruto Reflexos + Artefatos − Penalidade de armadura`.
- *Defesa Natural*: `Vigor + Bruto Fortitude + Escudo + Artefatos + Cobertura`.
- *Cobertura*: duas opções mutuamente exclusivas, **Parcial** (+5) e **Completa** (+10), ou nenhuma selecionada (+0). Alimenta a fórmula de Defesa Natural acima.
- *Redução Física*: `Artefato + Armadura`.
- *Redução Mágica*: `Artefato + Armadura mágica`.
- *Eficiência Elemental* e *Dano Elemental*: "[[Formulas]]" indica que ambos vêm de uma tabela, que ainda não foi escrita em nenhum documento do sistema. Campos previstos porém não implementados até a tabela existir.
- *Dano de Briga*: campo previsto porém sem fórmula definida ainda. Não implementar até a regra existir.

  

### 2.c) Afinidades

  

Ao contrário da lista fixa de Perícias, Afinidades é uma **lista incremental**: o jogador adiciona uma linha por vez, conforme necessário, para manter a ficha limpa. Segue o layout da tabela "- AFINIDADES -" da ficha em PDF (`Docs/Sistema RPG/Fichas/Ruína RPG - Ficha de personagem.pdf`): 3 blocos de campos por linha — Essência Básica, Sub-Elemento, Caminho e Experiência. Todos os campos abaixo são opcionais: uma linha pode ser adicionada só parcialmente preenchida (inclusive totalmente em branco) e completada depois — nenhum deles é obrigatório para adicionar ou manter a linha, e cada um permanece **NULL** até o jogador preencher.

- *Elemento*: dropdown fixo com os 4 elementos: **Fogo**, **Água**, **Terra**, **Ar** (ver "Matriz_Elemental.png" em `Docs/Sistema RPG`; "Mundano", presente na matriz apenas como referência, não é uma opção válida aqui).
- *Valor do Elemento*: campo numérico ≥ 0, sem relação de cálculo com os demais campos da linha.
- *Sub-Elemento*: dropdown fixo com os 14 Sub-Elementos da Matriz Elemental: Gelo, Raio, Prever, Ecomancia, Alma, Flora, Purificar, Hemomancia, Ferro, Curar, Necromancia, Vida, Aprimorar, Invocação. O cliente não filtra as opções pelo Elemento escolhido na mesma linha (evita esconder o campo até o Elemento ser preenchido); quando os dois já foram escolhidos, o servidor valida que o par Elemento/Sub-Elemento é um dos combináveis pela Matriz Elemental, rejeitando o resto.
- *Valor do Sub-Elemento*: campo numérico ≥ 0, mesma regra do Valor do Elemento.
- *Caminho e Experiência*: um nome livre, customizável pelo jogador (o "Caminho" trilhado dentro daquele Sub-Elemento) acompanhado de um valor numérico ≥ 0 (a "Experiência" naquele Caminho).

Qualquer campo de uma linha de Afinidade já existente pode ser editado pelo jogador a qualquer momento (não só no momento de adicionar a linha), e a linha pode ser removida a qualquer momento.

  

### 2.d) Perícias

  

A ficha exibe uma lista fixa das Perícias do sistema: Acrobacia, Alquimia, Arcano, Armadilhas, Armas Brancas, Artefatos Mágicos, Artístico, Atletismo, Avaliação, Biblioteca, Brigar, Condução, Conhecimentos, Crime, Empatia c/ Animais, Enganação, Força de Vontade, Fortitude, Furtividade, Herborismo, Intimidação, Intuição, Investigação, Lábia, Liderança, Linguística, Medicina, Navegação, Ocultismo, Ofício, Percepção, Pontaria, Prontidão, Reflexos, Religião, Saquear, Sedução, Senso Comum e Sobrevivência. Cada linha tem:

- *Gasto*: campo numérico ≥ 0, editável pelo jogador — pontos investidos naquela Perícia (ver "[[Tabela de Níveis]]" para os Pontos de Perícia concedidos por nível, e §2 de "[[Ruína RPG - Sistema Básico]]" para o ganho de pontos por Acerto Crítico em teste). Quando NULL, assume **0**. A soma do Gasto de todas as Perícias é exibida ao lado do total de Pontos de Perícia concedidos por nível (conforme a "[[Tabela de Níveis]]"), só como referência — diferente do orçamento de Atributos (2.a), não bloqueia o Gasto acima do valor da tabela.

Como um Acerto Crítico em teste também concede um ponto de Perícia (fora da tabela de níveis, então a soma acima eventualmente fica acima do máximo por um motivo legítimo), um campo dedicado permite adicionar ou subtrair a quantidade de pontos ganhos dessa forma (mesmo padrão do campo de XP em "Experiência atual", 1.b); esse valor é subtraído da soma de Gasto antes de compará-la ao total da tabela.
- *Modificador*: campo calculado, não editável. `Modificador = Gasto ÷ 3` (arredondado para baixo), conforme §2 de "[[Ruína RPG - Sistema Básico]]".
- *Atributo*: dropdown com os 8 atributos (ver 2.a). Não há um atributo-chave fixo por Perícia — a associação é situacional, escolhida pelo jogador conforme o teste sendo feito, e pode mudar de uma rolagem para outra.
- *Total*: campo calculado, não editável. `Total = Modificador + Total do Atributo escolhido` (ver 2.a), refletindo a Fórmula do Teste (`Dado da Cena + Modificador de Perícia + Atributo`) de §2 de "[[Ruína RPG - Sistema Básico]]" — sem o Dado da Cena, que é resolvido no momento da rolagem, fora da ficha.

3. **Combate**: armas e condutores, armaduras, escudos, efeito de batalha e iniciativa. Composta pelos subgrupos abaixo.

  

### 3.a) Armas e Condutores

  

Lista tipo arsenal: o jogador adiciona quantas armas quiser (inclui varinhas e cajados mágicos, que são subcategorias de Arma no catálogo — não há distinção de "condutor" à parte). Cada linha:

- *Arma*: dropdown/busca vinculado a um item do tipo Arma no "[[Requisitos - Catálogo de Itens e Equipamentos]]" (ver R0004). Ao escolher, os campos **Nome**, **Tipo de Dano**, **Alcance**, **Dados**, **Dano**, **Crítico** e **Tier** são preenchidos automaticamente a partir do item, somente leitura.
- *Peso*: herdado do item (somente leitura), somado ao Peso Total Carregado (ver 2.b, Movimentação) independentemente de estar equipada ou não.
- *Equipada*: seleção exclusiva — apenas 1 arma do arsenal pode estar marcada como equipada por vez, exceto se o personagem tiver a característica **Ambidestria** (ver aba "Posses", Características), que permite 2.
- *Durabilidade*: par **Atual / Máximo**. O Máximo é herdado do item (somente leitura, ver "[[Requisitos - Catálogo de Itens e Equipamentos]]" R0004) — diferente dos outros campos herdados, o Atual **não** é somente leitura: é editável pelo jogador e específico daquela linha (duas fichas com a mesma Arma do catálogo têm Durabilidade Atual independentes). Ao adicionar a linha, o Atual começa igual ao Máximo do item naquele momento; não pode exceder o Máximo.

Uma linha pode ser removida pelo jogador a qualquer momento.

  

### 3.b) Armaduras

  

Três slots fixos e sempre visíveis: **Capacete**, **Superior** e **Inferior**. Cada slot:

- *Armadura*: dropdown/busca vinculado a um item do tipo Armadura no "[[Requisitos - Catálogo de Itens e Equipamentos]]" (ver R0005). Ao escolher, os campos **Categoria**, **Defesa**, **RF**, **RM**, **Penalidade** e **Requisito de Vigor** são preenchidos automaticamente, somente leitura. Um slot pode ficar vazio (exibe travessão).
- *Peso*: herdado do item (somente leitura) de cada slot preenchido, somado ao Peso Total Carregado.
- *Durabilidade*: par **Atual / Máximo**, mesmo comportamento de 3.a — Máximo herdado do item (R0005), Atual editável pelo jogador e específico daquele slot.

  

### 3.c) Escudos

  

Lista tipo arsenal, mesmo padrão de 3.a: o jogador adiciona quantos escudos quiser. Cada linha:

- *Escudo*: dropdown/busca vinculado a um item do tipo Escudo no "[[Requisitos - Catálogo de Itens e Equipamentos]]" (ver R0006). Ao escolher, os campos **Nome**, **Categoria**, **Bônus de Defesa**, **Penalidade** e **Requisito de Vigor** são preenchidos automaticamente, somente leitura.
- *Peso*: herdado do item (somente leitura), somado ao Peso Total Carregado independentemente de estar equipado ou não.
- *Equipado*: seleção exclusiva — apenas 1 escudo do arsenal pode estar marcado como equipado por vez (sem exceção).
- *Durabilidade*: par **Atual / Máximo**, mesmo comportamento de 3.a — Máximo herdado do item (R0006), Atual editável pelo jogador e específico daquela linha.

Uma linha pode ser removida pelo jogador a qualquer momento.

  

### 3.d) Efeito de Batalha *(pendente)*

  

Campo previsto na ficha porém sem regra definida ainda no sistema. Não implementar até a regra existir.

  

### 3.e) Iniciativa

  

Exibe o valor do sub-atributo *Iniciativa* já calculado em 2.b (`Agilidade + Bruto Prontidão + Artefato ou item`), somente leitura. O cálculo final de Iniciativa em jogo soma esse valor ao máximo do Dado da Cena atual (ver §5 de "[[Ruína RPG - Sistema Básico]]"), resolvido manualmente pelo jogador no momento da cena, fora da ficha.

4. **Magias & Habilidades**: habilidade racial, magias/habilidades, contratos, runas e maestrias. Composta pelos subgrupos abaixo.

  

### 4.a) Habilidade Racial

  

Uma entrada fixa, não removível, pré-preenchida a partir da Linhagem/Variante escolhida em 1.a (ver §7 de "[[Ruína RPG - Sistema Básico]]" — ex: "Racial (Arca)" do Sinir/Laonir, "Racial (Lei da Selva)" do Phylac'tai/Es'Phylauc, "Racial (Sobre Voo)" do Yavos/Koroanos, "Racial (Amplificador Místico)" do Alóra). Campos:

- *Nome*: preenchido automaticamente com o nome do Racial da Variante escolhida. Exibe travessão se nenhuma Variante foi escolhida.
- *Tipo*: fixo em **Racial**, não editável.
- *Grau*: fixo em **9**, não editável.
- *Gasto em PI*: fixo em **N/A** — a Habilidade Racial não é comprada com Pontos de Ignição.
- *Custo*: valor em Foco para conjurar. O valor numérico específico de cada Habilidade Racial ainda não está definido nas regras — campo previsto porém não implementado até a regra existir.
- *Descrição*: preenchida automaticamente com o texto do Racial correspondente (ver §7 de "[[Ruína RPG - Sistema Básico]]").

  

### 4.b) Magias & Habilidades

  

Lista incremental: o jogador adiciona uma Magia ou Habilidade por vez, montando do zero ou partindo de uma entrada existente no "[[Requisitos - Banco de Magias e Habilidades]]" (que preenche os campos abaixo automaticamente). Toda entrada criada aqui, de qualquer uma das duas formas, é automaticamente salva como uma cópia independente naquele banco (ver lá, R0001). Cada entrada:

- *Nome*: text input.
- *Tipo*: dropdown, **Magia** ou **Habilidade**.
- *Grau*: campo numérico, limitado ao Grau/Círculo atual do personagem (ver 1.b). Determina quais Efeitos (abaixo) estão disponíveis para compra, conforme o Grau em que cada um é introduzido em "[[GRAUS & CÍRCULOS]]".
- *Efeitos*: lista dos Efeitos comprados para essa entrada — os 3 Efeitos Básicos universais (**Dano**, **Alcance**, **Duração**, cada um comprado em unidades até o teto do Grau escolhido, ver tabela no topo de "[[GRAUS & CÍRCULOS]]") e quaisquer Efeitos Especiais nomeados disponíveis até aquele Grau (ex: Aumentar Armadura, Cura, Deslocamento — ver "[[GRAUS & CÍRCULOS]]"). Alguns Efeitos Especiais exigem a compra prévia de outro Efeito (ex: "obrigatória a compra de Duração") — a interface deve impedir a compra de um Efeito cujo pré-requisito não foi comprado.
- *Gasto em PI*: campo calculado, não editável. Soma do custo em PI de todos os Efeitos comprados nessa entrada.
- *Custo*: campo calculado, não editável, em Foco (PF). `Custo = teto(1,25 × Gasto em PI)`, conforme "[[GRAUS & CÍRCULOS]]" ("Custo: 1,25 de arcana por PI").
- *Descrição*: texto livre.

Uma entrada pode ser removida pelo jogador a qualquer momento.

  

### 4.c) Contratos *(pendente)*

  

Contratos dependem da criatura invocada (ver "Contrato Mágico" em "[[GRAUS & CÍRCULOS]]" §1, que gera automaticamente uma Habilidade com Grau igual ao Rank da criatura). Como a Ficha de Criaturas ainda não foi detalhada, esta seção fica pendente até que aquele documento exista.

  

### 4.d) Runas

  

Runas são um encantamento das vocações não mágicas (Campeão e Caçador). Lista incremental: o jogador adiciona uma Runa por vez, com os campos:

- *Nome da Runa*: text input.
- *Descrição*: texto livre.
- *Grau*: campo numérico, limitado ao Grau atual do personagem (ver 1.b).

Uma Runa pode ser removida pelo jogador a qualquer momento.

  

### 4.e) Maestrias

  

Recurso separado da Maestria de Atributos (ver 2.a) — tem seu próprio poço de Pontos/Espaços de Maestria, concedido pela "[[Tabela de Níveis]]". Lista incremental: o jogador adiciona uma Maestria por vez, com os campos:

- *Nome*: text input.
- *Perícia*: dropdown com as Perícias (ver 2.d).
- *Atributo*: dropdown com os 8 atributos (ver 2.a).
- *Gasto Maestria*: campo numérico ≥ 0, editável — pontos investidos nessa Maestria. Limitado pelos Pontos de Maestria disponíveis (o total exato concedido por nível ainda não está totalmente definido — ver R0001 em 1.b, Âmbares Absorvidos, para outro gap semelhante).
- *Total*: campo calculado, não editável. `Total = Gasto Maestria + Bruto [Perícia escolhida] + [Atributo escolhido]` (ver "[[Formulas]]"; "Bruto [Perícia]" definido em 2.b).

Uma Maestria pode ser removida pelo jogador a qualquer momento.

5. **Posses**: inventário, artefatos, afeições e características (positivas/negativas). Composta pelos subgrupos abaixo.

  

6. **Diário**: anotações pessoais do jogador sobre a campanha. Ver detalhes abaixo.

  

### 5.a) Inventário

  

- *Ciclos*: campo numérico inteiro ≥ 0 — a moeda do sistema. Quando NULL, assume **0**.
- Lista incremental de itens carregados. Cada linha:
  - *Item*: dropdown/busca vinculado a um item do tipo Item Geral no "[[Requisitos - Catálogo de Itens e Equipamentos]]" (ver R0003). Armas, Armaduras e Escudos não aparecem aqui — eles têm suas próprias listas em 3.a/3.b/3.c.
  - *Peso*: herdado do item (somente leitura).
  - *Qtd*: campo numérico inteiro ≥ 1, editável.
  - *Total*: campo calculado, não editável. `Total = Peso × Qtd`. Soma ao Peso Total Carregado (ver 2.b, Movimentação).

Uma linha pode ser removida pelo jogador a qualquer momento.

  

### 5.b) Artefatos

  

O personagem pode equipar até **3 artefatos por tipo**, num total de até 12. Lista incremental, separada em 4 grupos (um por Tipo de alvo) de até 3 entradas cada. Cada entrada:

- *Artefato*: dropdown/busca vinculado a um item do tipo Artefato no "[[Requisitos - Catálogo de Itens e Equipamentos]]" (ver R0009). Ao escolher, os campos **Nome**, **Tipo de alvo**, **Alvo**, **Valor** e **Imagem** são preenchidos automaticamente a partir do item, somente leitura. O grupo em que a entrada entra (e o limite de 3) é determinado pelo Tipo de alvo do item escolhido — **Atributo**, **Perícia**, **Sub-Atributo** ou **Dano**.

A soma dos Valores de todos os Artefatos equipados de um dado Alvo alimenta o termo "Artefato(s)" nas fórmulas correspondentes (ver 2.a, 2.b e 2.d). Um Artefato pode ser removido pelo jogador a qualquer momento, respeitando o limite de 3 por Tipo de alvo.

  

### 5.c) Afeições

  

Lista incremental de vínculos de favorabilidade do personagem com NPCs ou criaturas específicas (normalmente NPCs). Cada linha:

- *Nome*: text input com o nome do NPC ou criatura, sem vínculo com "[[Requisitos - Ficha de NPCs]]" ou "[[Requisitos - Ficha de Criaturas]]" (é só um registro textual de com quem é a relação, não uma referência à ficha).
- *Favorabilidade*: campo numérico, editável pelo jogador (e possivelmente pelo GM). Quando NULL, assume **0**.

Uma linha pode ser removida pelo jogador a qualquer momento.

  

### 5.d) Características

  

Duas listas incrementais, **Positivas** e **Negativas**, de características escolhidas entre as listadas em "[[Características]]". Cada linha, em qualquer uma das duas listas:

- *Característica*: dropdown/busca vinculado a uma característica cadastrada em "[[Características]]", filtrado pela lista (Positiva ou Negativa) em que a linha está.
- *Custo*: herdado da característica escolhida (somente leitura, em pontos, ver "[[Características]]").

Cada lista exibe um **Total** (soma dos Custos daquela lista). Uma linha pode ser removida pelo jogador a qualquer momento.

Como o Custo das Negativas já é armazenado negativo (uma Negativa devolve pontos para gastar em Positivas), a soma dos dois Totais é o gasto líquido; ele é exibido ao lado do total de Pontos de Característica concedidos por nível (conforme a "[[Tabela de Níveis]]", campo "Espaço de Característica"), só como referência — assim como em Perícias (2.d), não bloqueia o Custo acima do valor da tabela.

  

## 6. Diário

  

Espaço do jogador para anotações pessoais sobre a campanha (pistas, lembretes, eventos importantes). Mesmo padrão do diário da Campanha (ver "[[Requisitos - Campanha]]" R0005): lista de entradas, cada uma com:

- *Data/hora*: preenchida automaticamente no momento da criação da entrada.
- *Texto*: conteúdo da entrada.
- *Imagens*: opcionais.

O jogador dono pode editar ou excluir qualquer entrada já publicada. Diferente do diário da Campanha (visível só ao GM), este é visível tanto ao jogador dono quanto ao GM.

  

# **R0002** - Ao subir de Nível, a ficha deve avisar o que foi recebido.

**Descrição**: Toda vez que o campo *Nível* (1.b) aumenta — pela Experiência Atual cruzando um novo limiar em "[[Tabelas de XP, Atributos, Características e EAP]]" — uma caixa de aviso aparece no topo da página, listando os bônus daquele novo Nível conforme a "[[Tabela de Níveis]]" (ex: "+9 Pontos de Atributo, +Status de Vida Aprimorado, +10 Pontos de Ignição..."). Se o personagem subir mais de um Nível de uma vez, a caixa lista os bônus de todos os Níveis ganhos, em ordem.

A caixa tem um botão de fechar; uma vez fechada pelo jogador, não reaparece — o estado "fechada" persiste (sobrevive a recarregar a página ou sair e voltar a entrar na ficha).

# **R0003** - Os campos de Item, Magia/Habilidade e Imagem só oferecem o que a campanha liberou.

> Quando um **jogador** edita a própria Ficha de Personagem, ou uma Ficha de NPC/Criatura que lhe foi concedida (Requisitos - Campanha R0010), os campos de 1.a) Imagem, 3.a)-3.c) Armas/Armaduras/Escudos, 4.b) Magias & Habilidades (ao reaproveitar uma entrada do Banco), 5.a)-5.b) Inventário/Artefatos e 6. Diário só listam Itens, entradas do Banco de Magias e Habilidades e Imagens que estão anexados à campanha daquela ficha **e** marcados como públicos (Requisitos - Campanha R0008/R0009) — não o catálogo/banco/biblioteca de imagens inteiro do GM vinculado.
>
> Um **GM** editando qualquer ficha (a própria, ou a de um jogador) continua com acesso irrestrito ao catálogo/banco/imagens, como hoje.